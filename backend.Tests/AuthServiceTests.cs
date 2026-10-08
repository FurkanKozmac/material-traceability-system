using System.Security.Cryptography;
using System.Text;
using backend.Common.Exceptions;
using backend.DTOs;
using backend.Entities;
using backend.Services.Implementations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace backend.Tests;

public class AuthServiceTests
{
    [Fact]
    public async Task Login_ValidCredentials_ReturnsTokenPair()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var user = new User { Username = "valid-user", Active = true };
        user.Password = PasswordHasher().HashPassword(user, "correct-password");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var result = await service.LoginAsync(new LoginRequest(user.Username, "correct-password", "DB"));

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
        Assert.Equal(result.AccessToken, result.Token);
    }

    [Fact]
    public async Task Login_WrongPassword_RejectsCredentials()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var user = new User { Username = "wrong-password-user", Active = true };
        user.Password = PasswordHasher().HashPassword(user, "correct-password");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var act = () => service.LoginAsync(new LoginRequest(user.Username, "incorrect-password", "DB"));

        // Assert
        await Assert.ThrowsAsync<BusinessRuleException>(act);
    }

    [Fact]
    public async Task Refresh_AccessToken_RejectsWrongTokenType()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var user = new User { Username = "access-refresh-user", Active = true };
        user.Password = PasswordHasher().HashPassword(user, "password");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var login = await service.LoginAsync(new LoginRequest(user.Username, "password", "DB"));

        // Act
        var act = () => service.RefreshAsync(login.AccessToken);

        // Assert
        await Assert.ThrowsAsync<BusinessRuleException>(act);
    }

    [Fact]
    public async Task Logout_ValidRefreshToken_RevokesStoredToken()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var user = new User { Username = "logout-user", Active = true };
        user.Password = PasswordHasher().HashPassword(user, "password");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var login = await service.LoginAsync(new LoginRequest(user.Username, "password", "DB"));

        // Act
        await service.RevokeRefreshTokenAsync(login.RefreshToken);

        // Assert
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(login.RefreshToken)));
        var storedToken = context.RefreshTokens.Single(token => token.TokenHash == hash);
        Assert.NotNull(storedToken.RevokedAt);
    }

    internal static AuthService CreateService(backend.Data.MtsDbContext context)
    {
        var settings = new Dictionary<string, string?>
        {
            ["Jwt:Key"] = "test-signing-key-at-least-thirty-two-bytes-long",
            ["Jwt:Issuer"] = "backend-tests",
            ["Jwt:Audience"] = "backend-tests",
            ["Jwt:AccessTokenMinutes"] = "30",
            ["Jwt:RefreshTokenDays"] = "7"
        };
        return new AuthService(
            context,
            new ConfigurationBuilder().AddInMemoryCollection(settings).Build(),
            PasswordHasher(),
            new MemoryCache(new MemoryCacheOptions()),
            new HttpContextAccessor());
    }

    private static IPasswordHasher<User> PasswordHasher() => new PasswordHasher<User>();
}
