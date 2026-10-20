using System.Text.Json.Serialization;
using backend.Services.Implementations.Notifications;
using System.Text;
using System.Net;
using System.Threading.RateLimiting;
using backend.Data;
using backend.Common.Security;
using backend.Entities;
using backend.Hubs;
using backend.Middlewares;
using backend.Services.Implementations;
using backend.Services.Implementations.Barcodes;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

var isDevelopment = builder.Environment.IsDevelopment();
var isProduction = builder.Environment.IsProduction();
var jwtSecret = builder.Configuration["JWT_SECRET"];
var adminPassword = builder.Configuration["ADMIN_PASSWORD"];
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

if (isProduction)
{
    ProductionConfigurationValidator.Validate(jwtSecret, adminPassword, connectionString);
}
else
{
    jwtSecret ??= "MTS-DEVELOPMENT-ONLY-KEY-2026-NEVER-USE-IN-PRODUCTION-64-CHARS";
    adminPassword ??= "admin123";
    connectionString ??= "Host=localhost;Port=5432;Database=mts_db;Username=postgres;Password=postgres";
}

builder.Configuration["Jwt:Key"] = jwtSecret;
builder.Configuration["ADMIN_PASSWORD"] = adminPassword;
builder.Configuration["ConnectionStrings:DefaultConnection"] = connectionString;


// ==========================================
// 1. SERVİS KAYITLARI (builder.Build ÖNCESİ)
// ==========================================

// CORS: React ve Mobil cihazların çerez/token ile bağlanmasına izin ver
var environmentOrigins = builder.Configuration["ALLOWED_ORIGINS"];
var configuredOrigins = !string.IsNullOrWhiteSpace(environmentOrigins)
    ? environmentOrigins.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    : builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientPolicy", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            // Development clients may run on localhost or a private LAN address.
            policy.SetIsOriginAllowed(IsDevelopmentOrigin);
        }
        else
        {
            policy.WithOrigins(configuredOrigins)
                .SetIsOriginAllowedToAllowWildcardSubdomains();
        }

        policy.AllowAnyMethod().AllowAnyHeader().AllowCredentials();
    });
});

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    // Caddy has this fixed address on the private Compose network. Do not trust arbitrary proxies.
    options.KnownProxies.Add(IPAddress.Parse("172.30.0.2"));
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    var loginLimit = builder.Configuration.GetValue("RateLimiting:Login:PermitLimit", LoginRateLimitPartition.DefaultPermitLimit);
    var loginWindowSeconds = builder.Configuration.GetValue("RateLimiting:Login:WindowSeconds", LoginRateLimitPartition.DefaultWindowSeconds);
    options.AddPolicy("login", context => LoginRateLimitPartition.Create(context, loginLimit, loginWindowSeconds));

    var safetyAssistantLimit = builder.Configuration.GetValue("RateLimiting:SafetyAssistant:PermitLimit", 10);
    var safetyAssistantWindowSeconds = builder.Configuration.GetValue("RateLimiting:SafetyAssistant:WindowSeconds", 60);
    options.AddPolicy("safety-assistant", context => LoginRateLimitPartition.Create(context, safetyAssistantLimit, safetyAssistantWindowSeconds));
});

// PostgreSQL Veritabanı
builder.Services.AddDbContext<MtsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient();
builder.Services.AddHttpClient<IOcrService, OcrService>(client =>
    client.Timeout = TimeSpan.FromSeconds(45));
builder.Services.AddScoped<ILabelParserService, LabelParserService>();
builder.Services.AddSignalR();

builder.Services.AddMtsAuthentication(builder.Configuration);
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireRole("Admin", "Operator")
        .Build());
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// Servislerimiz
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IChemicalService, ChemicalService>();
builder.Services.AddScoped<IUnitService, UnitService>();
builder.Services.AddScoped<IBatchService, BatchService>();
builder.Services.AddScoped<IAddressService, AddressService>();
builder.Services.AddScoped<IRelocationTaskService, RelocationTaskService>();
builder.Services.AddScoped<IMsdsRagService, MsdsRagService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddSingleton<ILowStockChannel, LowStockChannel>();
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.AddScoped<IEmailService, MailKitEmailService>();
builder.Services.AddHostedService<LowStockAlertBackgroundService>();
builder.Services.AddHostedService<RefreshTokenCleanupBackgroundService>();
builder.Services.AddScoped<IBarcodeGeneratorStrategy, QrCodeGeneratorStrategy>();
builder.Services.AddScoped<IBarcodeGeneratorStrategy, Code128GeneratorStrategy>();
builder.Services.AddScoped<BarcodeGeneratorFactory>();

// Controller & JSON Ayarları
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
});

// OpenAPI
builder.Services.AddOpenApi();

// ==========================================
// 2. UYGULAMA İNŞASI (BUILD)
// ==========================================
var app = builder.Build();

// ==========================================
// 3. HTTP BORU HATTI (app.Use... SIRALAMASI)
// ==========================================

// Forwarded headers are accepted only from the trusted Caddy container.
app.UseForwardedHeaders();
if (app.Environment.IsProduction())
{
    app.UseHsts();
    app.UseExceptionHandler(handler => handler.Run(async context =>
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            statusCode = StatusCodes.Status500InternalServerError,
            message = "Sunucu tarafında beklenmeyen bir hata oluştu."
        });
    }));
}
// Routing, CORS ve kimlik doğrulama sırası ASP.NET Core middleware gereksinimlerine göre ayarlanır.
app.UseRouting();
app.UseCors("ClientPolicy");
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<TraceabilityHub>("/hubs/traceability");

// Otomatik Migration Uygulama ve Veri Tohumlayıcı (Seeder)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<MtsDbContext>();
    await context.Database.MigrateAsync();
    try
    {
        await DbInitializer.SeedAsync(
            context,
            builder.Configuration,
            builder.Environment.IsDevelopment(),
            app.Logger);
    }
    catch (Exception ex)
    {
        app.Logger.LogCritical(ex, "Database seeding failed. The application will not start.");
        throw;
    }
}

app.Run();

static bool IsDevelopmentOrigin(string origin)
{
    if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        return false;

    if (uri.Host is "localhost" or "127.0.0.1" or "[::1]")
        return true;

    if (!IPAddress.TryParse(uri.Host, out var address) || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
        return false;

    var bytes = address.GetAddressBytes();
    return bytes[0] == 10 ||
           (bytes[0] == 172 && bytes[1] is >= 16 and <= 31) ||
           (bytes[0] == 192 && bytes[1] == 168);
}
