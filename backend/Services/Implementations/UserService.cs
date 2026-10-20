using System.Data;
using backend.Common.Exceptions;
using backend.Common.Security;
using backend.Data;
using backend.DTOs;
using backend.Entities;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace backend.Services.Implementations;

public sealed class UserService(
    MtsDbContext context,
    IPasswordHasher<User> passwordHasher) : IUserService
{
    private const string AdminRoleName = "Admin";
    private const string OperatorRoleName = "Operator";

    public async Task<List<UserResponse>> GetAllAsync(CancellationToken ct = default)
    {
        var users = await context.Users
            .AsNoTracking()
            .Include(user => user.Roles)
            .OrderBy(user => user.Username)
            .ToListAsync(ct);

        return users.Select(Map).ToList();
    }

    public async Task<UserResponse> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var user = await context.Users
            .AsNoTracking()
            .Include(item => item.Roles)
            .SingleOrDefaultAsync(item => item.Id == id, ct)
            ?? throw new NotFoundException("Kullanıcı bulunamadı.");

        return Map(user);
    }

    public async Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        ValidateRole(request.Role);

        var username = UsernamePolicy.NormalizeAndValidate(request.Username);
        var usernameLookup = username.ToLowerInvariant();
        PasswordPolicy.Validate(request.Password);

        var user = new User
        {
            Username = username,
            AuthSource = "DB",
            Active = true
        };
        user.Password = passwordHasher.HashPassword(user, request.Password);

        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var adminRole = await LockAdminRoleAsync(ct);

        if (await context.Users.AnyAsync(existing => existing.Username.ToLower() == usernameLookup, ct))
            throw new BusinessRuleException("Bu kullanıcı adı zaten kullanılıyor.", "USERNAME_ALREADY_EXISTS");

        var role = await GetRoleAsync(request.Role, adminRole, ct);
        user.Roles.Add(role);
        context.Users.Add(user);
        try
        {
            await context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new BusinessRuleException(
                "Bu kullanıcı adı zaten kullanılıyor.", "USERNAME_ALREADY_EXISTS");
        }
        await transaction.CommitAsync(ct);
        return Map(user);
    }

    public async Task<UserResponse> UpdateRoleAsync(
        long id, UpdateUserRoleRequest request, CancellationToken ct = default)
    {
        ValidateRole(request.Role);

        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        var adminRole = await LockAdminRoleAsync(ct);
        var user = await LockUserAsync(id, ct)
            ?? throw new NotFoundException("Kullanıcı bulunamadı.");

        var currentlyAdmin = user.Roles.Any(role => role.Name == AdminRoleName);
        var willBeAdmin = request.Role == AdminRoleName;
        var removesAdmin = currentlyAdmin && !willBeAdmin;

        if (removesAdmin && user.Active)
            await EnsureAnotherActiveAdminAsync(user.Id, ct);

        var role = await GetRoleAsync(request.Role, adminRole, ct);
        if (removesAdmin)
            await RefreshTokenRevocation.RevokeAllActiveAsync(context, user.Id, ct);

        user.Roles.Clear();
        user.Roles.Add(role);
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Map(user);
    }

    public async Task<UserResponse> UpdateActiveAsync(
        long id, UpdateUserActiveRequest request, long currentUserId, CancellationToken ct = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await LockAdminRoleAsync(ct);
        var user = await LockUserAsync(id, ct)
            ?? throw new NotFoundException("Kullanıcı bulunamadı.");

        if (id == currentUserId && !request.Active)
            throw new BusinessRuleException(
                "Kendi hesabınızı pasife alamazsınız.", "SELF_DEACTIVATION_FORBIDDEN");

        if (user.Active && !request.Active)
        {
            if (user.Roles.Any(role => role.Name == AdminRoleName))
                await EnsureAnotherActiveAdminAsync(user.Id, ct);
            await RefreshTokenRevocation.RevokeAllActiveAsync(context, user.Id, ct);
        }

        user.Active = request.Active;
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Map(user);
    }

    public async Task ResetPasswordAsync(
        long id, ResetUserPasswordRequest request, CancellationToken ct = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await LockAdminRoleAsync(ct);
        var user = await LockUserAsync(id, ct)
            ?? throw new NotFoundException("Kullanıcı bulunamadı.");

        PasswordPolicy.Validate(request.NewPassword);
        user.Password = passwordHasher.HashPassword(user, request.NewPassword);
        await RefreshTokenRevocation.RevokeAllActiveAsync(context, user.Id, ct);
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task DeleteAsync(long id, long currentUserId, CancellationToken ct = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        await LockAdminRoleAsync(ct);
        var user = await LockUserAsync(id, ct)
            ?? throw new NotFoundException("Kullanıcı bulunamadı.");

        if (id == currentUserId)
            throw new BusinessRuleException("Kendi hesabınızı silemezsiniz.", "SELF_DELETE_FORBIDDEN");

        if (user.Active && user.Roles.Any(role => role.Name == AdminRoleName))
            await EnsureAnotherActiveAdminAsync(user.Id, ct);

        await RefreshTokenRevocation.RevokeAllActiveAsync(context, user.Id, ct);
        context.Users.Remove(user);
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task<Role> LockAdminRoleAsync(CancellationToken ct)
    {
        if (context.Database.IsNpgsql())
        {
            return await context.Roles
                .FromSqlInterpolated($"SELECT * FROM roles WHERE \"Name\" = {AdminRoleName} FOR UPDATE")
                .SingleAsync(ct);
        }

        return await context.Roles.SingleAsync(role => role.Name == AdminRoleName, ct);
    }

    private async Task<User?> LockUserAsync(long id, CancellationToken ct)
    {
        if (context.Database.IsNpgsql())
        {
            var user = await context.Users
                .FromSqlInterpolated($"SELECT * FROM users WHERE \"Id\" = {id} FOR UPDATE")
                .SingleOrDefaultAsync(ct);
            if (user is not null)
                await context.Entry(user).Collection(item => item.Roles).LoadAsync(ct);
            return user;
        }

        return await context.Users
            .Include(item => item.Roles)
            .SingleOrDefaultAsync(item => item.Id == id, ct);
    }

    private async Task EnsureAnotherActiveAdminAsync(long targetId, CancellationToken ct)
    {
        var anotherActiveAdminExists = await context.Users.AnyAsync(
            user => user.Id != targetId &&
                    user.Active &&
                    user.Roles.Any(role => role.Name == AdminRoleName), ct);

        if (!anotherActiveAdminExists)
            throw new BusinessRuleException(
                "Sistemde en az bir aktif Admin kalmalıdır.", "LAST_ACTIVE_ADMIN");
    }

    private async Task<Role> GetRoleAsync(string roleName, Role adminRole, CancellationToken ct)
    {
        if (roleName == AdminRoleName)
            return adminRole;

        return await context.Roles.SingleOrDefaultAsync(role => role.Name == OperatorRoleName, ct)
            ?? throw new InvalidOperationException("Operator rolü bulunamadı.");
    }

    private static bool IsUniqueViolation(Exception? exception) =>
        exception is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } ||
        exception?.InnerException is not null && IsUniqueViolation(exception.InnerException);

    private static void ValidateRole(string roleName)
    {
        if (roleName != AdminRoleName && roleName != OperatorRoleName)
            throw new BusinessRuleException("Geçersiz rol.", "INVALID_ROLE");
    }

    private static UserResponse Map(User user)
    {
        var roles = user.Roles
            .Select(role => role.Name)
            .OrderBy(name => name == AdminRoleName ? 0 : 1)
            .ThenBy(name => name, StringComparer.Ordinal)
            .ToList();

        return new UserResponse(
            user.Id, user.Username, user.Active, roles.FirstOrDefault() ?? string.Empty, roles);
    }
}
