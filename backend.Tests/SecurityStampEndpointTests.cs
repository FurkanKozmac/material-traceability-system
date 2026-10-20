using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using backend.Common.Security;
using backend.Data;
using backend.DTOs;
using backend.Entities;
using backend.Services.Implementations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace backend.Tests;

public sealed class SecurityStampEndpointTests
{
    private const string JwtKey = "test-signing-key-at-least-thirty-two-bytes-long";
    private const string JwtIssuer = "backend-tests";
    private const string JwtAudience = "backend-tests";
    private static readonly IPasswordHasher<User> PasswordHasher = new PasswordHasher<User>();

    [Fact]
    public async Task ProtectedEndpoint_RejectsStaleClaimlessAndEmptyStampTokens_AndAllowsNewToken()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = CreateOptions(connection);
        await using var setupContext = new MtsDbContext(options);
        await setupContext.Database.EnsureCreatedAsync();
        var user = await AddUserAsync(setupContext, "stamp-endpoint-user", "Operator");

        var originalLogin = await AuthServiceTests.CreateService(setupContext).LoginAsync(
            new LoginRequest(user.Username, "current-password", "DB"));
        using var host = CreateHost(connection);
        using var client = host.GetTestClient();

        Assert.Equal(HttpStatusCode.OK, await GetStatusAsync(client, "protected", originalLogin.AccessToken));

        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await setupContext.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, await GetStatusAsync(
            client, "protected", originalLogin.AccessToken));

        var newLogin = await AuthServiceTests.CreateService(setupContext).LoginAsync(
            new LoginRequest(user.Username, "current-password", "DB"));
        Assert.Equal(HttpStatusCode.OK, await GetStatusAsync(client, "protected", newLogin.AccessToken));

        var claimlessToken = CreateTokenWithoutStamp(user.Id, user.Username, ["Operator"]);
        Assert.Equal(HttpStatusCode.Unauthorized, await GetStatusAsync(client, "protected", claimlessToken));
        Assert.Equal(HttpStatusCode.OK, await GetStatusAsync(client, "public", originalLogin.AccessToken));

        user.SecurityStamp = string.Empty;
        await setupContext.SaveChangesAsync();
        var emptyStampLogin = await AuthServiceTests.CreateService(setupContext).LoginAsync(
            new LoginRequest(user.Username, "current-password", "DB"));
        Assert.Equal(HttpStatusCode.Unauthorized, await GetStatusAsync(
            client, "protected", emptyStampLogin.AccessToken));
    }

    [Fact]
    public async Task Mutations_InvalidateOldAccessToken()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = CreateOptions(connection);
        await using var context = new MtsDbContext(options);
        await context.Database.EnsureCreatedAsync();
        var target = await AddUserAsync(context, "stamp-mutation-target", "Operator");
        var actor = await AddUserAsync(context, "stamp-mutation-actor", "Admin");
        using var host = CreateHost(connection);
        using var client = host.GetTestClient();

        var login = await AuthServiceTests.CreateService(context).LoginAsync(
            new LoginRequest(target.Username, "current-password", "DB"));
        Assert.Equal(HttpStatusCode.OK, await GetStatusAsync(client, "protected", login.AccessToken));

        var userService = new UserService(context, PasswordHasher);
        await userService.ResetPasswordAsync(target.Id, new ResetUserPasswordRequest("new-password-123"));
        Assert.Equal(HttpStatusCode.Unauthorized, await GetStatusAsync(client, "protected", login.AccessToken));

        var afterReset = await AuthServiceTests.CreateService(context).LoginAsync(
            new LoginRequest(target.Username, "new-password-123", "DB"));
        Assert.Equal(HttpStatusCode.OK, await GetStatusAsync(client, "protected", afterReset.AccessToken));

        await userService.UpdateRoleAsync(target.Id, new UpdateUserRoleRequest("Admin"));
        Assert.Equal(HttpStatusCode.Unauthorized, await GetStatusAsync(
            client, "protected", afterReset.AccessToken));

        var afterRoleChange = await AuthServiceTests.CreateService(context).LoginAsync(
            new LoginRequest(target.Username, "new-password-123", "DB"));
        Assert.Equal(HttpStatusCode.OK, await GetStatusAsync(client, "protected", afterRoleChange.AccessToken));

        await userService.UpdateActiveAsync(
            target.Id, new UpdateUserActiveRequest(false), actor.Id);
        Assert.Equal(HttpStatusCode.Unauthorized, await GetStatusAsync(
            client, "protected", afterRoleChange.AccessToken));
    }

    private static DbContextOptions<MtsDbContext> CreateOptions(SqliteConnection connection) =>
        new DbContextOptionsBuilder<MtsDbContext>().UseSqlite(connection).Options;

    private static async Task<User> AddUserAsync(MtsDbContext context, string username, string roleName)
    {
        var role = await context.Roles.SingleOrDefaultAsync(item => item.Name == roleName);
        if (role is null)
        {
            role = new Role { Name = roleName };
            context.Roles.Add(role);
        }

        var user = new User { Username = username, Active = true };
        user.Password = PasswordHasher.HashPassword(user, "current-password");
        user.Roles.Add(role);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static IHost CreateHost(SqliteConnection connection)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Key"] = JwtKey,
            ["Jwt:Issuer"] = JwtIssuer,
            ["Jwt:Audience"] = JwtAudience,
            ["Jwt:AccessTokenMinutes"] = "30",
            ["Jwt:RefreshTokenDays"] = "7"
        }).Build();

        return new HostBuilder()
            .ConfigureWebHost(webHost => webHost
                .UseTestServer()
                .ConfigureServices(services =>
                {
                    services.AddRouting();
                    services.AddControllers().AddApplicationPart(typeof(SecurityStampTestController).Assembly);
                    services.AddDbContext<MtsDbContext>(options => options.UseSqlite(connection));
                    services.AddMtsAuthentication(configuration);
                    services.AddAuthorization();
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

    private static async Task<HttpStatusCode> GetStatusAsync(
        HttpClient client, string route, string accessToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"/api/security-stamp-test/{route}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await client.SendAsync(request);
        return response.StatusCode;
    }

    private static string CreateTokenWithoutStamp(
        long userId, string username, IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ClaimTypes.Name, username),
            new("token_type", "access")
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey)),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: JwtIssuer,
            audience: JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

[ApiController]
[Route("api/security-stamp-test")]
public sealed class SecurityStampTestController : ControllerBase
{
    [Authorize]
    [HttpGet("protected")]
    public IActionResult Protected() => Ok(new { ok = true });

    [AllowAnonymous]
    [HttpGet("public")]
    public IActionResult Public() => Ok(new { ok = true });
}
