using System.Net;
using System.Text;
using backend.Common.Enums;
using backend.Data;
using backend.DTOs;
using backend.Entities;
using backend.Services.Implementations;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    [InlineData("PUT", "/api/settings/barcode-format")]
    [InlineData("PUT", "/api/settings/notifications")]
    [InlineData("POST", "/api/settings/test-alert-email")]
    public async Task Operator_IsForbidden_FromDeleteAndSettingsEndpoints(string method, string path)
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Test-Roles", "Operator");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
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
    public async Task UserManagementRoute_IsNotExposed()
    {
        using var host = CreateHost();
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/users")
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Test-Roles", "Operator");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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

    private static IHost CreateHost()
    {
        var chemical = new Mock<IChemicalService>();
        chemical.Setup(service => service.DeleteAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var addresses = new Mock<IAddressService>();
        var units = new Mock<IUnitService>();
        var settings = new Mock<ISettingsService>();
        var email = new Mock<IEmailService>();

        return new HostBuilder()
            .ConfigureWebHost(webHost => webHost
            .UseTestServer()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddAuthentication("Test")
                    .AddScheme<AuthenticationSchemeOptions, TestRoleAuthenticationHandler>("Test", _ => { });
                services.AddAuthorization(options =>
                {
                    options.FallbackPolicy = new AuthorizationPolicyBuilder("Test")
                        .RequireAuthenticatedUser()
                        .Build();
                });
                services.AddControllers().AddApplicationPart(typeof(backend.Controllers.ChemicalController).Assembly);
                services.AddSingleton(chemical.Object);
                services.AddSingleton(addresses.Object);
                services.AddSingleton(units.Object);
                services.AddSingleton(settings.Object);
                services.AddSingleton(email.Object);
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
            if (string.IsNullOrWhiteSpace(rolesHeader))
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
