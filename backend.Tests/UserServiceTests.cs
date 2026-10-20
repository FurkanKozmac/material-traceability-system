using backend.Common.Exceptions;
using backend.Data;
using backend.DTOs;
using backend.Entities;
using backend.Services.Implementations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests;

public class UserServiceTests
{
    private static readonly IPasswordHasher<User> PasswordHasher = new PasswordHasher<User>();

    [Fact]
    public async Task CreateAsync_HashesPasswordAndReturnsPasswordFreeResponse()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        await EnsureRolesAsync(context);
        var service = CreateService(context);

        var response = await service.CreateAsync(
            new CreateUserRequest("new-user", "new-password-123", "Operator"));

        Assert.Equal("new-user", response.Username);
        Assert.True(response.Active);
        Assert.Equal("Operator", response.Role);
        Assert.Contains("Operator", response.Roles);
        Assert.DoesNotContain(
            typeof(UserResponse).GetProperties(),
            property => property.Name.Contains("Password", StringComparison.OrdinalIgnoreCase));

        var user = await context.Users.SingleAsync(item => item.Username == "new-user");
        Assert.NotEqual("new-password-123", user.Password);
        Assert.StartsWith("AQAAAA", user.Password);
        Assert.Equal(
            PasswordVerificationResult.Success,
            PasswordHasher.VerifyHashedPassword(user, user.Password, "new-password-123"));
    }

    [Fact]
    public async Task CreateAsync_DuplicateUsername_IsRejected()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        await EnsureRolesAsync(context);
        await AddUserAsync(context, "duplicate-user", "Operator");
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new CreateUserRequest("duplicate-user", "new-password-123", "Operator")));

        Assert.Equal("USERNAME_ALREADY_EXISTS", exception.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_InvalidRole_IsRejected()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        await EnsureRolesAsync(context);
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new CreateUserRequest("new-user", "new-password-123", "Viewer")));

        Assert.Equal("INVALID_ROLE", exception.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_ShortPassword_IsRejected()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        await EnsureRolesAsync(context);
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new CreateUserRequest("short-password-user", "123456789", "Operator")));

        Assert.Equal("INVALID_PASSWORD", exception.ErrorCode);
        Assert.False(await context.Users.AnyAsync(user => user.Username == "short-password-user"));
    }

    [Fact]
    public async Task CreateAsync_TenCharacterPassword_IsAccepted()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        await EnsureRolesAsync(context);
        var service = CreateService(context);

        var response = await service.CreateAsync(
            new CreateUserRequest("ten-character-user", "1234567890", "Operator"));

        Assert.Equal("ten-character-user", response.Username);
        var user = await context.Users.SingleAsync(item => item.Username == "ten-character-user");
        Assert.Equal(
            PasswordVerificationResult.Success,
            PasswordHasher.VerifyHashedPassword(user, user.Password, "1234567890"));
    }

    [Fact]
    public async Task CreateAsync_UsernameWithInvalidCharacters_IsRejected()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        await EnsureRolesAsync(context);
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new CreateUserRequest("invalid user!", "new-password-123", "Operator")));

        Assert.Equal("INVALID_USERNAME", exception.ErrorCode);
    }

    [Fact]
    public async Task CreateAsync_UsernameTooShort_IsRejected()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        await EnsureRolesAsync(context);
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(new CreateUserRequest("ab", "new-password-123", "Operator")));

        Assert.Equal("INVALID_USERNAME", exception.ErrorCode);
    }

    [Fact]
    public async Task UpdateRoleAsync_AdminToOperator_RevokesActiveTokens()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        await EnsureRolesAsync(context);
        var admin = await AddUserAsync(context, "admin-1", "Admin");
        await AddUserAsync(context, "admin-2", "Admin");
        var activeToken = await AddRefreshTokenAsync(context, admin.Id, "active-admin-token");
        var revokedToken = await AddRefreshTokenAsync(
            context, admin.Id, "revoked-admin-token", DateTime.UtcNow.AddHours(-1));
        var previousRevokedAt = revokedToken.RevokedAt;
        var service = CreateService(context);

        var response = await service.UpdateRoleAsync(
            admin.Id, new UpdateUserRoleRequest("Operator"));

        Assert.Equal("Operator", response.Role);
        var updatedAdmin = await context.Users
            .Include(user => user.Roles)
            .SingleAsync(user => user.Id == admin.Id);
        var updatedRoles = updatedAdmin.Roles.Select(role => role.Name).ToList();
        Assert.Single(updatedRoles);
        Assert.Equal("Operator", updatedRoles[0]);
        Assert.NotNull(activeToken.RevokedAt);
        Assert.Equal(previousRevokedAt, revokedToken.RevokedAt);
    }

    [Fact]
    public async Task UpdateRoleAsync_LastActiveAdmin_IsRejected()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        await EnsureRolesAsync(context);
        var admin = await AddUserAsync(context, "only-admin", "Admin");
        var token = await AddRefreshTokenAsync(context, admin.Id, "last-admin-token");
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UpdateRoleAsync(admin.Id, new UpdateUserRoleRequest("Operator")));

        Assert.Equal("LAST_ACTIVE_ADMIN", exception.ErrorCode);
        var updatedAdmin = await context.Users
            .Include(user => user.Roles)
            .SingleAsync(user => user.Id == admin.Id);
        Assert.Contains("Admin", updatedAdmin.Roles.Select(role => role.Name));
        Assert.Null(token.RevokedAt);
    }

    [Fact]
    public async Task UpdateRoleAsync_OperatorToAdmin_DoesNotRevokeTokens()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        await EnsureRolesAsync(context);
        await AddUserAsync(context, "existing-admin", "Admin");
        var user = await AddUserAsync(context, "operator-user", "Operator");
        var token = await AddRefreshTokenAsync(context, user.Id, "operator-token");
        var service = CreateService(context);

        var response = await service.UpdateRoleAsync(user.Id, new UpdateUserRoleRequest("Admin"));

        Assert.Equal("Admin", response.Role);
        Assert.Null(token.RevokedAt);
    }

    [Fact]
    public async Task UpdateActiveAsync_Deactivate_RevokesActiveTokens()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        await EnsureRolesAsync(context);
        var admin = await AddUserAsync(context, "admin-to-deactivate", "Admin");
        var actor = await AddUserAsync(context, "acting-admin", "Admin");
        var token = await AddRefreshTokenAsync(context, admin.Id, "deactivate-token");
        var service = CreateService(context);

        var response = await service.UpdateActiveAsync(
            admin.Id, new UpdateUserActiveRequest(false), actor.Id);

        Assert.False(response.Active);
        Assert.NotNull(token.RevokedAt);
    }

    [Fact]
    public async Task UpdateActiveAsync_SelfDeactivate_IsRejected()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        await EnsureRolesAsync(context);
        var admin = await AddUserAsync(context, "self-deactivate-admin", "Admin");
        await AddUserAsync(context, "other-admin", "Admin");
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.UpdateActiveAsync(
                admin.Id, new UpdateUserActiveRequest(false), admin.Id));

        Assert.Equal("SELF_DEACTIVATION_FORBIDDEN", exception.ErrorCode);
        Assert.True((await context.Users.SingleAsync(user => user.Id == admin.Id)).Active);
    }

    [Fact]
    public async Task DeleteAsync_SelfDelete_IsRejected()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        await EnsureRolesAsync(context);
        var admin = await AddUserAsync(context, "self-delete-admin", "Admin");
        await AddUserAsync(context, "other-admin", "Admin");
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.DeleteAsync(admin.Id, admin.Id));

        Assert.Equal("SELF_DELETE_FORBIDDEN", exception.ErrorCode);
        Assert.True(await context.Users.AnyAsync(user => user.Id == admin.Id));
    }

    [Fact]
    public async Task DeleteAsync_LastActiveAdmin_IsRejected()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        await EnsureRolesAsync(context);
        var admin = await AddUserAsync(context, "last-active-admin", "Admin");
        var inactiveAdmin = await AddUserAsync(context, "inactive-admin", "Admin", active: false);
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.DeleteAsync(admin.Id, inactiveAdmin.Id));

        Assert.Equal("LAST_ACTIVE_ADMIN", exception.ErrorCode);
        Assert.True(await context.Users.AnyAsync(user => user.Id == admin.Id));
    }

    [Fact]
    public async Task DeleteAsync_ExistingUser_DeletesUser()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        await EnsureRolesAsync(context);
        var target = await AddUserAsync(context, "delete-target", "Operator");
        var actor = await AddUserAsync(context, "delete-actor", "Admin");
        await AddRefreshTokenAsync(context, target.Id, "delete-target-token");
        var service = CreateService(context);

        await service.DeleteAsync(target.Id, actor.Id);

        Assert.False(await context.Users.AnyAsync(user => user.Id == target.Id));
        Assert.False(await context.RefreshTokens.AnyAsync(token => token.UserId == target.Id));
    }

    [Fact]
    public async Task ResetPasswordAsync_UpdatesHashAndRevokesActiveTokens()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        await EnsureRolesAsync(context);
        var user = await AddUserAsync(context, "reset-password-user", "Operator");
        var token = await AddRefreshTokenAsync(context, user.Id, "reset-password-token");
        var service = CreateService(context);

        await service.ResetPasswordAsync(
            user.Id, new ResetUserPasswordRequest("reset-password-123"));

        var updatedUser = await context.Users.SingleAsync(item => item.Id == user.Id);
        Assert.StartsWith("AQAAAA", updatedUser.Password);
        Assert.Equal(
            PasswordVerificationResult.Success,
            PasswordHasher.VerifyHashedPassword(updatedUser, updatedUser.Password, "reset-password-123"));
        Assert.NotNull(token.RevokedAt);
    }

    [Fact]
    public async Task ResetPasswordAsync_ShortPassword_IsRejected()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        await EnsureRolesAsync(context);
        var user = await AddUserAsync(context, "reset-short-password-user", "Operator");
        var originalHash = user.Password;
        var service = CreateService(context);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ResetPasswordAsync(user.Id, new ResetUserPasswordRequest("123456789")));

        Assert.Equal("INVALID_PASSWORD", exception.ErrorCode);
        var storedUser = await context.Users.SingleAsync(item => item.Id == user.Id);
        Assert.Equal(originalHash, storedUser.Password);
    }

    [Fact]
    public async Task GetByIdAsync_MissingUser_ThrowsNotFound()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var service = CreateService(database.Context);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetByIdAsync(999));
    }

    private static UserService CreateService(MtsDbContext context) => new(context, PasswordHasher);

    private static async Task EnsureRolesAsync(MtsDbContext context)
    {
        if (!await context.Roles.AnyAsync(role => role.Name == "Admin"))
            context.Roles.Add(new Role { Name = "Admin" });
        if (!await context.Roles.AnyAsync(role => role.Name == "Operator"))
            context.Roles.Add(new Role { Name = "Operator" });
        await context.SaveChangesAsync();
    }

    private static async Task<User> AddUserAsync(
        MtsDbContext context, string username, string roleName, bool active = true)
    {
        var role = await context.Roles.SingleAsync(item => item.Name == roleName);
        var user = new User
        {
            Username = username,
            Active = active,
            AuthSource = "DB"
        };
        user.Password = PasswordHasher.HashPassword(user, "password-12345");
        user.Roles.Add(role);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<RefreshToken> AddRefreshTokenAsync(
        MtsDbContext context, long userId, string tokenHash, DateTime? revokedAt = null)
    {
        var token = new RefreshToken
        {
            UserId = userId,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            RevokedAt = revokedAt
        };
        context.RefreshTokens.Add(token);
        await context.SaveChangesAsync();
        return token;
    }
}
