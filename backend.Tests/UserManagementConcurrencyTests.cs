using backend.Common.Exceptions;
using backend.Data;
using backend.DTOs;
using backend.Entities;
using backend.Services.Implementations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests;

[Collection(PostgresTestCollection.Name)]
[Trait("Category", "Testcontainers")]
public class UserManagementConcurrencyTests(PostgresTestDatabase database)
{
    private static readonly IPasswordHasher<User> PasswordHasher = new PasswordHasher<User>();

    private delegate Task MutateUser(UserService service, long targetId, long actorId);

    [Fact]
    public async Task ConcurrentDelete_TwoAdmins_LeavesOneActiveAdmin()
    {
        await RunConcurrentLastAdminScenarioAsync(
            (service, targetId, actorId) => service.DeleteAsync(targetId, actorId));
    }

    [Fact]
    public async Task ConcurrentDeactivate_TwoAdmins_LeavesOneActiveAdmin()
    {
        await RunConcurrentLastAdminScenarioAsync(
            (service, targetId, actorId) =>
                service.UpdateActiveAsync(targetId, new UpdateUserActiveRequest(false), actorId));
    }

    [Fact]
    public async Task ConcurrentRoleDowngrade_TwoAdmins_LeavesOneActiveAdmin()
    {
        await RunConcurrentLastAdminScenarioAsync(
            (service, targetId, _) =>
                service.UpdateRoleAsync(targetId, new UpdateUserRoleRequest("Operator")));
    }

    private async Task RunConcurrentLastAdminScenarioAsync(MutateUser mutation)
    {
        var (admin1, admin2) = await PrepareTwoAdminsAsync();

        await using var context1 = database.CreateContext();
        await using var context2 = database.CreateContext();
        var service1 = new UserService(context1, PasswordHasher);
        var service2 = new UserService(context2, PasswordHasher);

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<bool> TryMutateAsync(UserService service, long targetId, long actorId)
        {
            await start.Task;
            try
            {
                await mutation(service, targetId, actorId);
                return true;
            }
            catch (BusinessRuleException exception) when (exception.ErrorCode == "LAST_ACTIVE_ADMIN")
            {
                return false;
            }
        }

        var first = TryMutateAsync(service1, admin2.Id, admin1.Id);
        var second = TryMutateAsync(service2, admin1.Id, admin2.Id);
        start.SetResult();

        var results = await Task.WhenAll(first, second).WaitAsync(TimeSpan.FromSeconds(20));

        Assert.Equal(1, results.Count(result => result));

        await using var verification = database.CreateContext();
        var activeAdminCount = await verification.Users.CountAsync(
            user => user.Active && user.Roles.Any(role => role.Name == "Admin"));
        Assert.Equal(1, activeAdminCount);
    }

    private async Task<(User Admin1, User Admin2)> PrepareTwoAdminsAsync()
    {
        await using var context = database.CreateContext();

        await context.Database.ExecuteSqlRawAsync("DELETE FROM user_roles");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM refresh_tokens");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM users");

        var adminRole = await context.Roles.SingleOrDefaultAsync(role => role.Name == "Admin");
        if (adminRole is null)
        {
            adminRole = new Role { Name = "Admin" };
            context.Roles.Add(adminRole);
        }
        if (!await context.Roles.AnyAsync(role => role.Name == "Operator"))
            context.Roles.Add(new Role { Name = "Operator" });
        await context.SaveChangesAsync();

        var admin1 = CreateAdmin($"concurrency-admin-a-{Guid.NewGuid():N}", adminRole);
        var admin2 = CreateAdmin($"concurrency-admin-b-{Guid.NewGuid():N}", adminRole);
        context.Users.AddRange(admin1, admin2);
        await context.SaveChangesAsync();

        return (admin1, admin2);
    }

    private static User CreateAdmin(string username, Role adminRole)
    {
        var user = new User
        {
            Username = username,
            Active = true,
            AuthSource = "DB"
        };
        user.Password = PasswordHasher.HashPassword(user, "password-12345");
        user.Roles.Add(adminRole);
        return user;
    }
}
