using System.Text.Json.Serialization;
using System.Text;
using System.Net;
using System.Threading.RateLimiting;
using backend.Data;
using backend.Entities;
using backend.Middlewares;
using backend.Services.Implementations;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using Microsoft.AspNetCore.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. SERVİS KAYITLARI (builder.Build ÖNCESİ)
// ==========================================

// CORS: React ve Mobil cihazların çerez/token ile bağlanmasına izin ver
builder.Services.AddCors(options =>
{
    options.AddPolicy("ClientPolicy", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(IsDevelopmentOrigin);
        }
        else
        {
            var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
            policy.WithOrigins(allowedOrigins);
        }

        policy.AllowAnyMethod().AllowAnyHeader().AllowCredentials();
    });
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
            AutoReplenishment = true
        }));
});

// PostgreSQL Veritabanı
builder.Services.AddDbContext<MtsDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key yapılandırılmalıdır.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = context =>
            {
                if (context.Principal?.FindFirst("token_type")?.Value != "access")
                    context.Fail("Bu uç için access token gereklidir.");
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

// Servislerimiz
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IChemicalService, ChemicalService>();
builder.Services.AddScoped<IUnitService, UnitService>();
builder.Services.AddScoped<IBatchService, BatchService>();
builder.Services.AddScoped<IAddressService, AddressService>();
builder.Services.AddScoped<IRelocationTaskService, RelocationTaskService>();

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

// CORS ve Hata Yakalama EN BAŞTA olmalı!
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

// Otomatik Migration Uygulama ve Veri Tohumlayıcı (Seeder)
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<MtsDbContext>();
    await context.Database.MigrateAsync();
    await DbInitializer.SeedAsync(context);
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
