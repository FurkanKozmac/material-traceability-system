using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using backend.Data;
using backend.DTOs;
using backend.Entities;
using backend.Middlewares;
using backend.Services.Implementations;
using backend.Services.Implementations.Notifications;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace backend.Tests;

[Collection(PostgresTestCollection.Name)]
[Trait("Category", "Testcontainers")]
public class RelocationTaskEndpointPostgresTests(PostgresTestDatabase database)
{
    [Fact]
    public async Task Operator_CreatesRelocationTask_AndReceivesCreatedTask()
    {
        // Arrange
        await using var context = database.CreateContext();
        var (_, _, _, unit) = await TestData.AddUnitAsync(context);
        var target = new Address { Code = $"ADDRESS-{Guid.NewGuid():N}", StorageType = "GENERAL", MaxCapacity = 5 };
        context.Addresses.Add(target);
        await context.SaveChangesAsync();

        using var host = CreateHost(services =>
        {
            services.AddDbContext<MtsDbContext>(options => options.UseNpgsql(database.ConnectionString));
            services.AddSingleton<ILowStockChannel, LowStockChannel>();
            services.AddScoped<IRelocationTaskService, RelocationTaskService>();
        });
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/relocation-tasks")
        {
            Content = JsonContent.Create(new CreateRelocationTaskRequest(unit.Id, target.Id))
        };
        request.Headers.Add(HeaderRoleAuthenticationHandler.RolesHeader, "Operator");

        // Act
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{(int)response.StatusCode}: {body}");
        var payload = JsonSerializer.Deserialize<JsonElement>(body);
        Assert.True(payload.GetProperty("id").GetInt64() > 0);
        Assert.Equal("Pending", payload.GetProperty("status").GetString());
        Assert.Equal(unit.Barcode, payload.GetProperty("unitBarcode").GetString());
        Assert.Equal(target.Code, payload.GetProperty("toAddressCode").GetString());
    }

    internal static IHost CreateHost(Action<IServiceCollection> configureServices, string environmentName = "Development") =>
        new HostBuilder()
            .UseEnvironment(environmentName)
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddAuthentication(HeaderRoleAuthenticationHandler.SchemeName)
                        .AddScheme<AuthenticationSchemeOptions, HeaderRoleAuthenticationHandler>(
                            HeaderRoleAuthenticationHandler.SchemeName, _ => { });
                    services.AddAuthorization(options =>
                    {
                        options.FallbackPolicy = new AuthorizationPolicyBuilder(HeaderRoleAuthenticationHandler.SchemeName)
                            .RequireAuthenticatedUser()
                            .RequireRole("Admin", "Operator")
                            .Build();
                    });
                    services.AddControllers().AddApplicationPart(typeof(backend.Controllers.RelocationTasksController).Assembly);
                    configureServices(services);
                })
                .Configure(app =>
                {
                    app.UseRouting();
                    app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
                    app.UseAuthentication();
                    app.UseAuthorization();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                }))
            .Start();
}

internal sealed class HeaderRoleAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "HeaderRoles";
    public const string RolesHeader = "X-Test-Roles";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var roles = Request.Headers[RolesHeader].ToString();
        if (string.IsNullOrWhiteSpace(roles))
            return Task.FromResult(AuthenticateResult.NoResult());

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "42"), new(ClaimTypes.Name, "endpoint-test") };
        claims.AddRange(roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(role => new Claim(ClaimTypes.Role, role)));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
