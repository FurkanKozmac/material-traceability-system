using System.Security.Cryptography;
using System.Text;
using backend.Common.Exceptions;
using backend.DTOs;
using backend.Entities;
using backend.Services.Implementations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
        var user = CreateUser("valid-user", "correct-password");
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
    public async Task Login_UsernameWithDifferentCase_ReturnsTokenPair()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var user = CreateUser("CaseSensitiveLoginUser", "correct-password");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var result = await service.LoginAsync(
            new LoginRequest("casesensitiveloginuser", "correct-password", "DB"));

        // Assert
        Assert.Equal("CaseSensitiveLoginUser", result.Username);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
    }

    [Fact]
    public async Task Login_WrongPassword_RejectsCredentials()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var user = CreateUser("wrong-password-user", "correct-password");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var act = () => service.LoginAsync(new LoginRequest(user.Username, "incorrect-password", "DB"));

        // Assert
        await Assert.ThrowsAsync<BusinessRuleException>(act);
    }

    [Fact]
    public async Task Login_UserWithoutRole_IsRejectedWithoutIssuingToken()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var user = CreateUser("roleless-user", "password", role: null);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var act = () => service.LoginAsync(new LoginRequest(user.Username, "password", "DB"));

        // Assert
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(act);
        Assert.Equal("USER_HAS_NO_ROLE", exception.ErrorCode);
        Assert.Empty(context.RefreshTokens);
    }

    [Fact]
    public async Task Login_PlaintextPassword_IsRejected()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var user = new User
        {
            Username = "plaintext-user",
            Password = "plaintext-password",
            Active = true,
            Roles = [new Role { Name = "Operator" }]
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var act = () => service.LoginAsync(new LoginRequest(user.Username, "plaintext-password", "DB"));

        // Assert
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(act);
        Assert.Equal("Geçersiz kullanıcı adı veya şifre!", exception.Message);
        Assert.Empty(context.RefreshTokens);
    }

    [Fact]
    public async Task ChangePassword_ValidCurrentPassword_UpdatesHashAndRevokesTokens()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var user = CreateUser("change-password-user", "current-password");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var login = await service.LoginAsync(
            new LoginRequest(user.Username, "current-password", "DB"));
        var originalSecurityStamp = user.SecurityStamp;

        // Act
        await service.ChangePasswordAsync(
            new ChangePasswordRequest(user.Username, "current-password", "new-password-123"));

        // Assert
        context.ChangeTracker.Clear();
        var updatedUser = await context.Users.SingleAsync(item => item.Id == user.Id);
        Assert.StartsWith("AQAAAA", updatedUser.Password);
        Assert.NotEqual(originalSecurityStamp, updatedUser.SecurityStamp);
        Assert.NotEqual(
            PasswordVerificationResult.Failed,
            PasswordHasher().VerifyHashedPassword(updatedUser, updatedUser.Password, "new-password-123"));

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(login.RefreshToken)));
        var storedToken = await context.RefreshTokens.SingleAsync(token => token.TokenHash == hash);
        Assert.NotNull(storedToken.RevokedAt);
    }

    [Fact]
    public async Task ChangePassword_WrongCurrentPassword_IsRejectedWithoutChangingHash()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var user = CreateUser("wrong-current-password-user", "current-password");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var originalHash = user.Password;
        var service = CreateService(context);

        // Act
        var act = () => service.ChangePasswordAsync(
            new ChangePasswordRequest(user.Username, "wrong-password", "new-password-123"));

        // Assert
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(act);
        Assert.Equal("Geçersiz kullanıcı adı veya parola!", exception.Message);
        var updatedUser = await context.Users.SingleAsync(item => item.Id == user.Id);
        Assert.Equal(originalHash, updatedUser.Password);
    }

    [Fact]
    public async Task ChangePassword_BCryptCurrentPassword_IsAcceptedAndHashUpgraded()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var user = new User
        {
            Username = "bcrypt-change-password-user",
            Password = BCrypt.Net.BCrypt.HashPassword("current-password"),
            Active = true,
            Roles = [new Role { Name = "Operator" }]
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        await service.ChangePasswordAsync(
            new ChangePasswordRequest(user.Username, "current-password", "new-password-123"));

        // Assert
        context.ChangeTracker.Clear();
        var updatedUser = await context.Users.SingleAsync(item => item.Id == user.Id);
        Assert.StartsWith("AQAAAA", updatedUser.Password);
        Assert.NotEqual(
            PasswordVerificationResult.Failed,
            PasswordHasher().VerifyHashedPassword(updatedUser, updatedUser.Password, "new-password-123"));
    }

    [Fact]
    public async Task Login_InactiveUser_WithCorrectPassword_ReturnsGenericFailure()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var user = CreateUser("inactive-login-user", "correct-password");
        user.Active = false;
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var act = () => service.LoginAsync(new LoginRequest(user.Username, "correct-password", "DB"));

        // Assert
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(act);
        Assert.Equal("Geçersiz kullanıcı adı veya şifre!", exception.Message);
        Assert.Empty(context.RefreshTokens);
    }

    [Fact]
    public async Task Login_RolelessUser_WithWrongPassword_ReturnsGenericFailure()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var user = CreateUser("roleless-wrong-password-user", "correct-password", role: null);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var act = () => service.LoginAsync(new LoginRequest(user.Username, "wrong-password", "DB"));

        // Assert
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(act);
        Assert.Equal("Geçersiz kullanıcı adı veya şifre!", exception.Message);
        Assert.Null(exception.ErrorCode);
    }

    [Fact]
    public async Task ChangePassword_UnknownUser_ReturnsGenericFailure()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var service = CreateService(database.Context);

        // Act
        var act = () => service.ChangePasswordAsync(
            new ChangePasswordRequest("unknown-change-password-user", "password", "new-password-123"));

        // Assert
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(act);
        Assert.Equal("Geçersiz kullanıcı adı veya parola!", exception.Message);
        Assert.Null(exception.ErrorCode);
    }

    [Fact]
    public async Task ChangePassword_InactiveUser_ReturnsGenericFailure()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var user = CreateUser("inactive-change-password-user", "current-password");
        user.Active = false;
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var originalHash = user.Password;
        var service = CreateService(context);

        // Act
        var act = () => service.ChangePasswordAsync(
            new ChangePasswordRequest(user.Username, "current-password", "new-password-123"));

        // Assert
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(act);
        Assert.Equal("Geçersiz kullanıcı adı veya parola!", exception.Message);
        Assert.Equal(
            originalHash,
            (await context.Users.SingleAsync(item => item.Id == user.Id)).Password);
    }

    [Fact]
    public async Task ChangePassword_RolelessUser_ReturnsGenericFailure()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var user = CreateUser("roleless-change-password-user", "current-password", role: null);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var originalHash = user.Password;
        var service = CreateService(context);

        // Act
        var act = () => service.ChangePasswordAsync(
            new ChangePasswordRequest(user.Username, "current-password", "new-password-123"));

        // Assert
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(act);
        Assert.Equal("Geçersiz kullanıcı adı veya parola!", exception.Message);
        Assert.Equal(
            originalHash,
            (await context.Users.SingleAsync(item => item.Id == user.Id)).Password);
    }

    [Fact]
    public async Task ChangePassword_NewPasswordTooShort_IsRejected()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var user = CreateUser("short-new-password-user", "current-password");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var act = () => service.ChangePasswordAsync(
            new ChangePasswordRequest(user.Username, "current-password", "12345678901"));

        // Assert
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(act);
        Assert.Equal("INVALID_PASSWORD", exception.ErrorCode);
    }

    [Fact]
    public async Task ChangePassword_NewPasswordSameAsCurrent_IsRejected()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var user = CreateUser("same-new-password-user", "current-password");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var act = () => service.ChangePasswordAsync(
            new ChangePasswordRequest(user.Username, "current-password", "current-password"));

        // Assert
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(act);
        Assert.Equal("PASSWORD_UNCHANGED", exception.ErrorCode);
    }

    [Fact]
    public async Task ChangePassword_RepeatedWrongPasswords_TriggerLoginFailureCounter()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var user = CreateUser("change-password-lockout-user", "current-password");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failure = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.ChangePasswordAsync(
                    new ChangePasswordRequest(user.Username, "wrong-password", "new-password-123")));
            Assert.Equal("Geçersiz kullanıcı adı veya parola!", failure.Message);
        }

        var lockout = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ChangePasswordAsync(
                new ChangePasswordRequest(user.Username, "current-password", "new-password-123")));

        // Assert
        Assert.Contains("Çok fazla başarısız giriş denemesi", lockout.Message);
    }

    [Fact]
    public async Task Login_RepeatedWrongPasswordsWithCaseVariations_TriggersLockout()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        context.Users.Add(CreateUser("case-variation-lockout-user", "correct-password"));
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var usernameVariations = new[]
        {
            "case-variation-lockout-user",
            "Case-Variation-Lockout-User",
            " CASE-VARIATION-LOCKOUT-USER ",
            "CASE-variation-LOCKOUT-user",
            " case-variation-lockout-user "
        };

        // Act
        foreach (var username in usernameVariations)
        {
            var failure = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.LoginAsync(new LoginRequest(username, "wrong-password", "DB")));
            Assert.Equal("Geçersiz kullanıcı adı veya şifre!", failure.Message);
        }

        var lockout = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.LoginAsync(new LoginRequest("CaSe-VaRiAtIoN-LoCkOuT-UsEr", "correct-password", "DB")));

        // Assert
        Assert.Contains("Çok fazla başarısız giriş denemesi", lockout.Message);
    }

    [Fact]
    public async Task LoginAndChangePassword_ShareCaseInsensitiveFailureCounter()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        context.Users.Add(CreateUser("shared-case-lockout-user", "correct-password"));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var username = attempt % 2 == 0
                ? "shared-case-lockout-user"
                : " SHARED-CASE-LOCKOUT-USER ";
            var failure = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.LoginAsync(new LoginRequest(username, "wrong-password", "DB")));
            Assert.Equal("Geçersiz kullanıcı adı veya şifre!", failure.Message);
        }

        // Act
        var lockout = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ChangePasswordAsync(
                new ChangePasswordRequest("Shared-Case-Lockout-User", "correct-password", "new-password-123")));

        // Assert
        Assert.Contains("Çok fazla başarısız giriş denemesi", lockout.Message);
    }

    [Fact]
    public async Task ChangePassword_UnknownUser_RepeatedWrongPasswords_ReturnsSameLockoutMessageAsExistingUser()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        context.Users.Add(CreateUser("existing-change-lockout-user", "current-password"));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var existingUserLockout = await TriggerChangePasswordLockoutAsync(
            service, "existing-change-lockout-user");
        var unknownUserLockout = await TriggerChangePasswordLockoutAsync(
            service, "unknown-change-lockout-user");

        // Assert
        Assert.Contains("Çok fazla başarısız giriş denemesi", unknownUserLockout);
        Assert.Equal(existingUserLockout, unknownUserLockout);
    }

    [Fact]
    public async Task Login_UnknownUser_RepeatedWrongPasswords_ReturnsSameLockoutMessageAsExistingUser()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        context.Users.Add(CreateUser("existing-login-lockout-user", "current-password"));
        await context.SaveChangesAsync();
        var service = CreateService(context);

        // Act
        var existingUserLockout = await TriggerLoginLockoutAsync(service, "existing-login-lockout-user");
        var unknownUserLockout = await TriggerLoginLockoutAsync(service, "unknown-login-lockout-user");

        // Assert
        Assert.Contains("Çok fazla başarısız giriş denemesi", unknownUserLockout);
        Assert.Equal(existingUserLockout, unknownUserLockout);
    }

    [Fact]
    public async Task Refresh_StaleSecurityStamp_IsRejected()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var user = CreateUser("stale-refresh-stamp-user", "correct-password");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var login = await service.LoginAsync(
            new LoginRequest(user.Username, "correct-password", "DB"));

        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await context.SaveChangesAsync();

        // Act
        var act = () => service.RefreshAsync(login.RefreshToken);

        // Assert
        await Assert.ThrowsAsync<BusinessRuleException>(act);
    }

    [Fact]
    public async Task Refresh_AccessToken_RejectsWrongTokenType()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var user = CreateUser("access-refresh-user", "password");
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
    public async Task Refresh_UserWithoutRole_IsRejectedWithoutRotatingToken()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var user = CreateUser("refresh-roleless-user", "password");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var service = CreateService(context);
        var login = await service.LoginAsync(new LoginRequest(user.Username, "password", "DB"));

        user.Roles.Clear();
        await context.SaveChangesAsync();

        // Act
        var act = () => service.RefreshAsync(login.RefreshToken);

        // Assert
        var exception = await Assert.ThrowsAsync<BusinessRuleException>(act);
        Assert.Equal("USER_HAS_NO_ROLE", exception.ErrorCode);
        var storedToken = Assert.Single(context.RefreshTokens);
        Assert.Null(storedToken.RevokedAt);
        Assert.Null(storedToken.ReplacedByTokenHash);
    }

    [Fact]
    public async Task Logout_ValidRefreshToken_RevokesStoredToken()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var user = CreateUser("logout-user", "password");
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

    private static async Task<string> TriggerChangePasswordLockoutAsync(AuthService service, string username)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failure = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.ChangePasswordAsync(
                    new ChangePasswordRequest(username, "wrong-password", "new-password-123")));
            Assert.Equal("Geçersiz kullanıcı adı veya parola!", failure.Message);
        }

        var lockout = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ChangePasswordAsync(
                new ChangePasswordRequest(username, "current-password", "new-password-123")));
        return lockout.Message;
    }

    private static async Task<string> TriggerLoginLockoutAsync(AuthService service, string username)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var failure = await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.LoginAsync(new LoginRequest(username, "wrong-password", "DB")));
            Assert.Equal("Geçersiz kullanıcı adı veya şifre!", failure.Message);
        }

        var lockout = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.LoginAsync(new LoginRequest(username, "current-password", "DB")));
        return lockout.Message;
    }

    private static User CreateUser(string username, string password, string? role = "Operator")
    {
        var user = new User { Username = username, Active = true };
        if (role is not null)
            user.Roles.Add(new Role { Name = role });
        user.Password = PasswordHasher().HashPassword(user, password);
        return user;
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
