using System.Net;
using System.Reflection;
using System.Text;
using System.Net.Http.Json;
using backend.Common.Enums;
using backend.Common.Security;
using backend.Data;
using backend.DTOs;
using backend.Entities;
using backend.Services.Implementations;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace backend.Tests;

public sealed class AuthorizationEndpointTests
{
    [Theory]
    [InlineData("DELETE", "/api/chemicals/42")]
    [InlineData("DELETE", "/api/addresses/42")]
    [InlineData("DELETE", "/api/units/UNIT-42")]
    [InlineData("POST", "/api/chemicals")]
    [InlineData("POST", "/api/addresses")]
    [InlineData("POST", "/api/units")]
    [InlineData("PUT", "/api/settings/barcode-format")]
    [InlineData("PUT", "/api/settings/notifications")]
    [InlineData("POST", "/api/settings/test-alert-email")]
    [InlineData("GET", "/api/users")]
    [InlineData("GET", "/api/users/42")]
    [InlineData("POST", "/api/users")]
    [InlineData("PUT", "/api/users/42/role")]
    [InlineData("PUT", "/api/users/42/active")]
    [InlineData("POST", "/api/users/42/reset-password")]
    [InlineData("DELETE", "/api/users/42")]
    public async Task Operator_IsForbidden_FromAdminOnlyEndpoints(string method, string path)
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (!string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase))
        {
            request.Content = path == "/api/batches/scan-label"
                ? CreateMultipartImageContent()
                : new StringContent("{}", Encoding.UTF8, "application/json");
        }
        request.Headers.Add("X-Test-Roles", "Operator");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static MultipartFormDataContent CreateMultipartImageContent()
    {
        var content = new MultipartFormDataContent();
        var image = new ByteArrayContent([1, 2, 3]);
        image.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(image, "image", "label.png");
        return content;
    }

    [Fact]
    public async Task Operator_CanCompleteRelocationTask()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/relocation-tasks/7/complete")
        {
            Content = new StringContent(
                "{\"unitBarcode\":\"BAR-DEMO-UNIT-004\",\"targetAddressBarcode\":\"ADR-DEMO-RACK-SOL-02\"}",
                Encoding.UTF8,
                "application/json")
        };
        request.Headers.Add("X-Test-Roles", "Operator");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Operator_CanConsumeSeededDemoUnit()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/units/BAR-DEMO-UNIT-001/consume");
        request.Headers.Add("X-Test-Roles", "Operator");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Operator_CanScanLabelAndReceiveParsedSuggestions()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();
        using var content = CreateMultipartImageContent();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/batches/scan-label") { Content = content };
        request.Headers.Add("X-Test-Roles", "Operator");

        using var response = await client.SendAsync(request);
        var result = await response.Content.ReadFromJsonAsync<BatchLabelScanResultDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("OCR-LOT-42", result?.ExtractedLotNumber);
        Assert.Equal("BAT-OCR-LOT-42", result?.SuggestedBarcodePayload);
    }

    [Fact]
    public async Task Operator_CanCreateBatch()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/batches")
        {
            Content = JsonContent.Create(new CreateBatchRequest(
                "LOT-RECEIVED-42", "Fictional Supplier", 4,
                new DateTime(2027, 11, 15, 0, 0, 0, DateTimeKind.Utc), 1))
        };
        request.Headers.Add("X-Test-Roles", "Operator");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Operator_CanCreateRelocationTask()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/relocation-tasks")
        {
            Content = JsonContent.Create(new CreateRelocationTaskRequest(7, 12))
        };
        request.Headers.Add("X-Test-Roles", "Operator");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutIdentity_ReturnsUnauthorized()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();
        using var response = await client.GetAsync("/api/chemicals");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedUserWithoutRole_IsForbidden_OnStandardGetEndpoint()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/chemicals");
        request.Headers.Add("X-Test-Authenticated", "true");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AuthenticatedUserWithoutRole_IsForbidden_OnUserManagementEndpoint()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users");
        request.Headers.Add("X-Test-Authenticated", "true");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousEndpoints_WithoutToken_ReturnSuccess()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();

        using var healthResponse = await client.GetAsync("/api/health");
        using var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login", new LoginRequest("demo", "demo123", "DB"));
        using var refreshResponse = await client.PostAsJsonAsync(
            "/api/auth/refresh", new RefreshTokenRequest("refresh-token"));
        using var changePasswordResponse = await client.PostAsJsonAsync(
            "/api/auth/change-password",
            new ChangePasswordRequest("demo", "demo123", "new-demo-password"));
        using var safetyResponse = await client.PostAsJsonAsync(
            "/api/safetyassistant/ask", new SafetyQuestionRequest("CHEM-1", "How should it be stored?"));

        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, changePasswordResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, safetyResponse.StatusCode);
    }

    [Fact]
    public void ChangePasswordEndpoint_UsesLoginRateLimitPolicy()
    {
        var method = typeof(backend.Controllers.AuthController)
            .GetMethod(nameof(backend.Controllers.AuthController.ChangePassword));
        var attribute = method?.GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal("login", attribute.PolicyName);
    }

    [Fact]
    public async Task Admin_CanListUsers()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/users");
        request.Headers.Add("X-Test-Roles", "Admin");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Admin_CanDeleteChemical()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Delete, "/api/chemicals/42");
        request.Headers.Add("X-Test-Roles", "Admin");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task DemoSeed_CanSignInWithOperatorRole()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ADMIN_PASSWORD"] = "test-admin-password-123",
            ["DEMO_USER_ENABLED"] = "true"
        }).Build();
        await DbInitializer.SeedAsync(database.Context, configuration, isDevelopment: false);

        var login = await AuthServiceTests.CreateService(database.Context)
            .LoginAsync(new LoginRequest("demo", "demo123", "DB"));

        Assert.Equal("Operator", login.Role);
        Assert.Contains("Operator", login.Roles);
        Assert.DoesNotContain("Admin", login.Roles);
    }

    private static IConfiguration TestAuthenticationConfiguration() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "test-signing-key-at-least-thirty-two-bytes-long",
            ["Jwt:Issuer"] = "backend-tests",
            ["Jwt:Audience"] = "backend-tests"
        }).Build();

    private static IHost CreateHost()
    {
        var chemical = new Mock<IChemicalService>();
        chemical.Setup(service => service.DeleteAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var addresses = new Mock<IAddressService>();
        var units = new Mock<IUnitService>();
        units.Setup(service => service.ConsumeAsync("BAR-DEMO-UNIT-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UnitResponse(1, "BAR-DEMO-UNIT-001", "Depleted", null, null, 1,
                "DEMO-BAT-001", "Polymer Stabilizer M-01", "DEMO-CHM-01", DateTime.UtcNow.AddDays(180), false));
        var settings = new Mock<ISettingsService>();
        var email = new Mock<IEmailService>();
        var relocationTasks = new Mock<IRelocationTaskService>();
        relocationTasks.Setup(service => service.CreateTaskAsync(
                It.IsAny<CreateRelocationTaskRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RelocationTaskResponse(7, "Pending", DateTime.UtcNow, null,
                1, "BAR-DEMO-UNIT-001", 3, "ADR-DEMO-RACK-01", 12,
                "ADR-DEMO-RACK-SOL-02", null, null));
        relocationTasks.Setup(service => service.CompleteTaskAsync(
                It.IsAny<long>(), It.IsAny<CompleteRelocationTaskRequest>(), It.IsAny<long?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var ocr = new Mock<IOcrService>();
        ocr.Setup(service => service.ExtractTextAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("SODIUM HYDROXIDE LOT: OCR-LOT-42 EXP: 2027-11-15");
        var labelParser = new Mock<ILabelParserService>();
        labelParser.Setup(service => service.ParseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BatchLabelScanResultDto(1, "Sodium Hydroxide", "OCR-LOT-42",
                new DateTime(2027, 11, 15, 0, 0, 0, DateTimeKind.Utc),
                "SODIUM HYDROXIDE LOT: OCR-LOT-42 EXP: 2027-11-15", "BAT-OCR-LOT-42"));
        var batches = new Mock<IBatchService>();
        batches.Setup(service => service.CreateAsync(It.IsAny<CreateBatchRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BatchResponse(42, "LOT-RECEIVED-42", "Fictional Supplier", 4,
                new DateTime(2027, 11, 15, 0, 0, 0, DateTimeKind.Utc), 1, "Sodium Hydroxide", 4));
        var loginResponse = new LoginResponse(
            "access-token", "access-token", "refresh-token", "demo", "Operator",
            ["Operator"], new UserDto(1, "demo", "Operator", ["Operator"]));
        var auth = new Mock<IAuthService>();
        auth.Setup(service => service.LoginAsync(It.IsAny<LoginRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(loginResponse);
        auth.Setup(service => service.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(loginResponse);
        auth.Setup(service => service.ChangePasswordAsync(
                It.IsAny<ChangePasswordRequest>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var users = new Mock<IUserService>();
        users.Setup(service => service.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var rag = new Mock<IMsdsRagService>();
        rag.Setup(service => service.AskQuestionAsync(
                It.IsAny<SafetyQuestionRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SafetyAnswerResponse(
                "CHEM-1", "How should it be stored?", "Store it safely.", "Storage", true));

        return new HostBuilder()
            .ConfigureWebHost(webHost => webHost
            .UseTestServer()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddMtsAuthentication(TestAuthenticationConfiguration());
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestRoleAuthenticationHandler>("Test", _ => { });
                services.AddAuthorization(options =>
                {
                    options.FallbackPolicy = new AuthorizationPolicyBuilder("Test")
                        .RequireAuthenticatedUser()
                        .RequireRole("Admin", "Operator")
                        .Build();
                });
                // BatchesController broadcasts inventory changes after creation.
                // The production host registers SignalR in Program.cs; this isolated
                // test host must register it as well so controller activation matches.
                services.AddSignalR();
                services.AddControllers().AddApplicationPart(typeof(backend.Controllers.ChemicalController).Assembly);
                services.AddDbContext<MtsDbContext>(options => options.UseSqlite("Data Source=:memory:"));
                services.AddSingleton(chemical.Object);
                services.AddSingleton(addresses.Object);
                services.AddSingleton(units.Object);
                services.AddSingleton(settings.Object);
                services.AddSingleton(email.Object);
                services.AddSingleton(relocationTasks.Object);
                services.AddSingleton(batches.Object);
                services.AddSingleton(ocr.Object);
                services.AddSingleton(labelParser.Object);
                services.AddSingleton(auth.Object);
                services.AddSingleton(users.Object);
                services.AddSingleton(rag.Object);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            }))
            .Start();
    }

    private sealed class TestRoleAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var rolesHeader = Request.Headers["X-Test-Roles"].ToString();
            var authenticatedWithoutRoles = string.Equals(
                Request.Headers["X-Test-Authenticated"].ToString(),
                "true",
                StringComparison.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(rolesHeader) && !authenticatedWithoutRoles)
                return Task.FromResult(AuthenticateResult.NoResult());

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, "42"),
                new(ClaimTypes.Name, "authorization-test")
            };
            claims.AddRange(rolesHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(role => new Claim(ClaimTypes.Role, role)));
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);
            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
