using backend.Common.Exceptions;
using backend.Data;
using backend.DTOs;
using backend.Entities;
using backend.Services.Implementations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace backend.Tests;

[Collection(PostgresTestCollection.Name)]
[Trait("Category", "Testcontainers")]
public class UsernameUniquenessTests(PostgresTestDatabase database)
{
    private static readonly IPasswordHasher<User> PasswordHasher = new PasswordHasher<User>();

    [Fact]
    public async Task RawSqlInsert_DifferentCaseUsername_ThrowsUniqueViolation()
    {
        await using var context = database.CreateContext();
        var username = $"Case-{Guid.NewGuid():N}";
        const string password = "not-used-in-this-test";

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO users (\"Username\", \"Password\", auth_source, active) VALUES ({username}, {password}, 'DB', true)");

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO users (\"Username\", \"Password\", auth_source, active) VALUES ({username.ToLowerInvariant()}, {password}, 'DB', true)"));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, exception.SqlState);
    }

    [Fact]
    public async Task CreateAsync_DatabaseUniqueViolation_ReturnsUsernameAlreadyExists()
    {
        await using var blockingContext = database.CreateContext();
        await using var serviceContext = database.CreateContext();
        await EnsureRolesAsync(blockingContext);

        var username = $"Race-{Guid.NewGuid():N}";
        const string password = "not-used-in-this-test";

        await using var blockingTransaction = await blockingContext.Database.BeginTransactionAsync();
        await blockingContext.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO users (\"Username\", \"Password\", auth_source, active) VALUES ({username.ToUpperInvariant()}, {password}, 'DB', true)");

        var service = new UserService(serviceContext, PasswordHasher);
        var createTask = service.CreateAsync(
            new CreateUserRequest(username.ToLowerInvariant(), "new-password-123", "Operator"));

        await Task.Delay(TimeSpan.FromMilliseconds(500));
        await blockingTransaction.CommitAsync();

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => createTask);
        Assert.Equal("USERNAME_ALREADY_EXISTS", exception.ErrorCode);
    }

    [Fact]
    public async Task Migration_CaseInsensitiveConflict_FailsWithConflictDetails()
    {
        var databaseName = $"mts_case_conflict_{Guid.NewGuid():N}";
        var adminConnectionString = database.ConnectionString;

        await ExecuteAdminCommandAsync(
            adminConnectionString, $"CREATE DATABASE \"{databaseName}\"");

        try
        {
            var connectionStringBuilder = new NpgsqlConnectionStringBuilder(adminConnectionString)
            {
                Database = databaseName
            };
            var options = new DbContextOptionsBuilder<MtsDbContext>()
                .UseNpgsql(connectionStringBuilder.ConnectionString)
                .Options;
            await using var context = new MtsDbContext(options);

            var migrations = context.Database.GetMigrations().ToList();
            await context.GetService<IMigrator>().MigrateAsync(migrations[^2]);

            var suffix = Guid.NewGuid().ToString("N");
            var firstUsername = $"CaseConflict-{suffix}";
            var secondUsername = firstUsername.ToLowerInvariant();
            const string password = "not-used-in-this-test";
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO users (\"Username\", \"Password\", auth_source, active) VALUES ({firstUsername}, {password}, 'DB', true)");
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO users (\"Username\", \"Password\", auth_source, active) VALUES ({secondUsername}, {password}, 'DB', true)");

            var exception = await Assert.ThrowsAnyAsync<Exception>(() =>
                context.GetService<IMigrator>().MigrateAsync(migrations[^1]));

            Assert.Contains("case-insensitive username conflicts", exception.Message);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await ExecuteAdminCommandAsync(
                adminConnectionString, $"DROP DATABASE IF EXISTS \"{databaseName}\" WITH (FORCE)");
        }
    }

    private static async Task EnsureRolesAsync(MtsDbContext context)
    {
        if (!await context.Roles.AnyAsync(role => role.Name == "Admin"))
            context.Roles.Add(new Role { Name = "Admin" });
        if (!await context.Roles.AnyAsync(role => role.Name == "Operator"))
            context.Roles.Add(new Role { Name = "Operator" });
        await context.SaveChangesAsync();
    }

    private static async Task ExecuteAdminCommandAsync(string connectionString, string commandText)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(commandText, connection);
        await command.ExecuteNonQueryAsync();
    }
}
