using backend.Data;
using backend.Entities;
using backend.Hubs;
using backend.Services.Implementations;
using backend.Services.Implementations.Notifications;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;

namespace backend.Tests;

public sealed class DemoDataSeederTests
{
    [Fact]
    public async Task DemoDataDisabled_DoesNotSeedBusinessData_ButStillSeedsAdmin()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var configuration = Configuration(demoDataEnabled: false, demoUserEnabled: false);

        await DbInitializer.SeedAsync(database.Context, configuration, isDevelopment: false);

        Assert.True(await database.Context.Users.AnyAsync(user => user.Username == "admin" && user.Active));
        Assert.Contains("Admin", await database.Context.Roles.Select(role => role.Name).ToListAsync());
        Assert.Empty(await database.Context.Chemicals.ToListAsync());
        Assert.Empty(await database.Context.Batches.ToListAsync());
        Assert.Empty(await database.Context.Addresses.ToListAsync());
        Assert.Empty(await database.Context.Units.ToListAsync());
        Assert.Empty(await database.Context.RelocationTasks.ToListAsync());
    }

    [Fact]
    public async Task DemoDataDefaultsOnInDevelopmentAndOffInProduction()
    {
        await using var developmentDatabase = await SqliteTestDatabase.CreateAsync();
        await DbInitializer.SeedAsync(developmentDatabase.Context, Configuration(), isDevelopment: true);
        Assert.Equal(12, await developmentDatabase.Context.Chemicals.CountAsync());

        await using var productionDatabase = await SqliteTestDatabase.CreateAsync();
        await DbInitializer.SeedAsync(productionDatabase.Context, Configuration(), isDevelopment: false);
        Assert.Empty(await productionDatabase.Context.Chemicals.ToListAsync());
    }

    [Fact]
    public async Task DemoDataSeed_UsesRelativeExpiryDatesAndFictionalSupplierNames()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await DbInitializer.SeedAsync(database.Context, Configuration(demoDataEnabled: true), isDevelopment: false);
        var now = DateTime.UtcNow;
        var nearExpiry = await database.Context.Batches.SingleAsync(batch => batch.BatchNo == "DEMO-BAT-002");
        var expired = await database.Context.Batches.SingleAsync(batch => batch.BatchNo == "DEMO-BAT-003");

        Assert.InRange(nearExpiry.ExpirationDate!.Value, now.AddDays(9), now.AddDays(11));
        Assert.InRange(expired.ExpirationDate!.Value, now.AddDays(-6), now.AddDays(-4));
        Assert.All(await database.Context.Batches.Select(batch => batch.Supplier).ToListAsync(),
            supplier => Assert.StartsWith("Kurgusal Demo Tedarikçisi", supplier));
        Assert.Equal(12, await database.Context.Chemicals.CountAsync());
        Assert.Equal(10, await database.Context.Addresses.CountAsync());
        Assert.Equal(8, await database.Context.Batches.CountAsync());
        Assert.Equal(21, await database.Context.Units.CountAsync());
        Assert.Equal(4, await database.Context.RelocationTasks.CountAsync(task => task.Status == "Pending"));

        var belowThreshold = await database.Context.Chemicals
            .Select(chemical => new
            {
                chemical.MinStockThreshold,
                InStock = chemical.Batches.SelectMany(batch => batch.Units)
                    .Count(unit => unit.Status == "InStock" || unit.Status == "AVAILABLE")
            })
            .CountAsync(item => item.InStock < item.MinStockThreshold);
        Assert.True(belowThreshold >= 8);
    }

    [Fact]
    public async Task DemoDataSeed_IsIdempotentAndFillsMissingRowsWithoutOverwritingExistingRows()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        await DbInitializer.SeedAsync(context, Configuration(demoDataEnabled: true), isDevelopment: false);
        var initialCounts = await CountsAsync(context);
        await DbInitializer.SeedAsync(context, Configuration(demoDataEnabled: true), isDevelopment: false);
        Assert.Equal(initialCounts, await CountsAsync(context));

        var seededGeneralChemical = await context.Chemicals.SingleAsync(item => item.ChemicalCode == "DEMO-CHM-01");
        seededGeneralChemical.Name = "Existing demo key must not be overwritten";
        var optionalChemical = await context.Chemicals.SingleAsync(item => item.ChemicalCode == "DEMO-CHM-12");
        context.Chemicals.Remove(optionalChemical);
        await context.SaveChangesAsync();

        await DbInitializer.SeedAsync(context, Configuration(demoDataEnabled: true), isDevelopment: false);

        Assert.Equal(initialCounts, await CountsAsync(context));
        Assert.Equal("Existing demo key must not be overwritten",
            (await context.Chemicals.SingleAsync(item => item.ChemicalCode == "DEMO-CHM-01")).Name);
        Assert.True(await context.Chemicals.AnyAsync(item => item.ChemicalCode == "DEMO-CHM-12"));
    }

    [Fact]
    public async Task SeededDemoUnit_CanBeConsumed()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        await DbInitializer.SeedAsync(context, Configuration(demoDataEnabled: true), isDevelopment: false);
        var hub = CreateHub();
        var service = new UnitService(context, hub.Object, new LowStockChannel());

        var consumed = await service.ConsumeAsync("UNIT-DEMO-UNIT-001");

        Assert.Equal("Depleted", consumed.Status);
        Assert.Null(consumed.AddressId);
    }

    [Fact]
    public async Task ExistingAdminPassword_IsNotChanged_WhenResetFlagIsOff()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;

        await DbInitializer.SeedAsync(
            context,
            Configuration(adminPassword: "first-admin-password"),
            isDevelopment: false);
        var originalHash = (await context.Users.SingleAsync(user => user.Username == "admin")).Password;

        await DbInitializer.SeedAsync(
            context,
            Configuration(adminPassword: "second-admin-password"),
            isDevelopment: false);

        context.ChangeTracker.Clear();
        var admin = await context.Users.SingleAsync(user => user.Username == "admin");
        Assert.Equal(originalHash, admin.Password);
        Assert.True(BCrypt.Net.BCrypt.Verify("first-admin-password", admin.Password));
        Assert.False(BCrypt.Net.BCrypt.Verify("second-admin-password", admin.Password));
    }

    [Fact]
    public async Task AdminPasswordReset_ResetsPasswordRevokesActiveTokensAndLogsWarning()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        await DbInitializer.SeedAsync(
            context,
            Configuration(adminPassword: "old-admin-password"),
            isDevelopment: false);

        var admin = await context.Users.SingleAsync(user => user.Username == "admin");
        var activeToken = new RefreshToken
        {
            UserId = admin.Id,
            TokenHash = "active-admin-token",
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };
        var revokedToken = new RefreshToken
        {
            UserId = admin.Id,
            TokenHash = "revoked-admin-token",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            RevokedAt = DateTime.UtcNow.AddHours(-1)
        };
        context.RefreshTokens.AddRange(activeToken, revokedToken);
        await context.SaveChangesAsync();
        var previousRevokedAt = revokedToken.RevokedAt;

        var logger = new ListLogger();
        await DbInitializer.SeedAsync(
            context,
            Configuration(adminPassword: "new-admin-password", adminPasswordReset: true),
            isDevelopment: false,
            logger);

        context.ChangeTracker.Clear();
        var updatedAdmin = await context.Users.SingleAsync(user => user.Username == "admin");
        var updatedActiveToken = await context.RefreshTokens
            .SingleAsync(token => token.TokenHash == "active-admin-token");
        var updatedRevokedToken = await context.RefreshTokens
            .SingleAsync(token => token.TokenHash == "revoked-admin-token");

        Assert.True(BCrypt.Net.BCrypt.Verify("new-admin-password", updatedAdmin.Password));
        Assert.False(BCrypt.Net.BCrypt.Verify("old-admin-password", updatedAdmin.Password));
        Assert.NotNull(updatedActiveToken.RevokedAt);
        Assert.Equal(previousRevokedAt, updatedRevokedToken.RevokedAt);
        Assert.Contains(logger.Messages, message =>
            message.Contains("WARNING: ADMIN_PASSWORD_RESET flag is ACTIVE!", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, message =>
            message.Contains("new-admin-password", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeletedAdmin_IsNotRecreated_WhenAnotherAdminExists()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        await DbInitializer.SeedAsync(
            context,
            Configuration(adminPassword: "initial-admin-password"),
            isDevelopment: false);
        await AddAdminAsync(context, "other-admin");

        var admin = await context.Users.SingleAsync(user => user.Username == "admin");
        context.Users.Remove(admin);
        await context.SaveChangesAsync();

        await DbInitializer.SeedAsync(
            context,
            Configuration(adminPassword: ""),
            isDevelopment: false);

        Assert.False(await context.Users.AnyAsync(user => user.Username == "admin"));
        Assert.True(await context.Users.AnyAsync(user => user.Username == "other-admin"));
    }

    [Fact]
    public async Task DemotedAdmin_IsNotRestored_WhenAnotherAdminExists()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        await DbInitializer.SeedAsync(
            context,
            Configuration(adminPassword: "initial-admin-password"),
            isDevelopment: false);
        await AddAdminAsync(context, "other-admin");

        var admin = await context.Users.Include(user => user.Roles)
            .SingleAsync(user => user.Username == "admin");
        var operatorRole = await context.Roles.SingleAsync(role => role.Name == "Operator");
        admin.Roles.Clear();
        admin.Roles.Add(operatorRole);
        admin.Active = false;
        await context.SaveChangesAsync();
        var originalHash = admin.Password;

        await DbInitializer.SeedAsync(
            context,
            Configuration(adminPassword: "different-admin-password"),
            isDevelopment: false);

        context.ChangeTracker.Clear();
        var reloadedAdmin = await context.Users.Include(user => user.Roles)
            .SingleAsync(user => user.Username == "admin");
        Assert.DoesNotContain(reloadedAdmin.Roles, role => role.Name == "Admin");
        Assert.Contains(reloadedAdmin.Roles, role => role.Name == "Operator");
        Assert.Equal(originalHash, reloadedAdmin.Password);
        Assert.False(reloadedAdmin.Active);
    }

    [Fact]
    public async Task AdminPasswordReset_RestoresDeletedAdminWithNewPassword()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        await DbInitializer.SeedAsync(
            context,
            Configuration(adminPassword: "initial-admin-password"),
            isDevelopment: false);

        var admin = await context.Users.SingleAsync(user => user.Username == "admin");
        context.Users.Remove(admin);
        await context.SaveChangesAsync();

        await DbInitializer.SeedAsync(
            context,
            Configuration(adminPassword: "reset-admin-password-123", adminPasswordReset: true),
            isDevelopment: false);

        context.ChangeTracker.Clear();
        var restoredAdmin = await context.Users.Include(user => user.Roles)
            .SingleAsync(user => user.Username == "admin");
        Assert.True(restoredAdmin.Active);
        Assert.Contains(restoredAdmin.Roles, role => role.Name == "Admin");
        Assert.True(BCrypt.Net.BCrypt.Verify("reset-admin-password-123", restoredAdmin.Password));
    }

    [Fact]
    public async Task AdminPasswordReset_ReactivatesInactiveAdminWithNewPassword()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        await DbInitializer.SeedAsync(
            context,
            Configuration(adminPassword: "initial-admin-password"),
            isDevelopment: false);

        var admin = await context.Users.SingleAsync(user => user.Username == "admin");
        admin.Active = false;
        await context.SaveChangesAsync();

        await DbInitializer.SeedAsync(
            context,
            Configuration(adminPassword: "reset-admin-password-123", adminPasswordReset: true),
            isDevelopment: false);

        context.ChangeTracker.Clear();
        var restoredAdmin = await context.Users.Include(user => user.Roles)
            .SingleAsync(user => user.Username == "admin");
        Assert.True(restoredAdmin.Active);
        Assert.Contains(restoredAdmin.Roles, role => role.Name == "Admin");
        Assert.True(BCrypt.Net.BCrypt.Verify("reset-admin-password-123", restoredAdmin.Password));
    }

    [Fact]
    public async Task AdminPasswordReset_RestoresAdminRoleForRolelessAdmin()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        await DbInitializer.SeedAsync(
            context,
            Configuration(adminPassword: "initial-admin-password"),
            isDevelopment: false);

        var admin = await context.Users.Include(user => user.Roles)
            .SingleAsync(user => user.Username == "admin");
        admin.Roles.Clear();
        await context.SaveChangesAsync();

        await DbInitializer.SeedAsync(
            context,
            Configuration(adminPassword: "reset-admin-password-123", adminPasswordReset: true),
            isDevelopment: false);

        context.ChangeTracker.Clear();
        var restoredAdmin = await context.Users.Include(user => user.Roles)
            .SingleAsync(user => user.Username == "admin");
        Assert.True(restoredAdmin.Active);
        Assert.Contains(restoredAdmin.Roles, role => role.Name == "Admin");
        Assert.True(BCrypt.Net.BCrypt.Verify("reset-admin-password-123", restoredAdmin.Password));
    }

    [Fact]
    public async Task NoAdminExists_SeedCreatesAdminWithConfiguredPassword()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;

        await DbInitializer.SeedAsync(
            context,
            Configuration(adminPassword: "bootstrap-admin-password-123"),
            isDevelopment: false);

        var admin = await context.Users.Include(user => user.Roles)
            .SingleAsync(user => user.Username == "admin");
        Assert.True(admin.Active);
        Assert.Contains(admin.Roles, role => role.Name == "Admin");
        Assert.True(BCrypt.Net.BCrypt.Verify("bootstrap-admin-password-123", admin.Password));
    }

    [Fact]
    public async Task InactiveAdminOnly_SeedRestoresAdminAccount()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        await DbInitializer.SeedAsync(
            context,
            Configuration(adminPassword: "initial-admin-password"),
            isDevelopment: false);

        var admin = await context.Users.SingleAsync(user => user.Username == "admin");
        admin.Active = false;
        await context.SaveChangesAsync();

        await DbInitializer.SeedAsync(
            context,
            Configuration(adminPassword: "restored-admin-password-123"),
            isDevelopment: false);

        context.ChangeTracker.Clear();
        var restoredAdmin = await context.Users.Include(user => user.Roles)
            .SingleAsync(user => user.Username == "admin");
        Assert.True(restoredAdmin.Active);
        Assert.Contains(restoredAdmin.Roles, role => role.Name == "Admin");
        Assert.True(BCrypt.Net.BCrypt.Verify("restored-admin-password-123", restoredAdmin.Password));
    }

    [Fact]
    public async Task BootstrapRestore_RevokesActiveTokensAndLogsWarningWithoutPassword()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        await DbInitializer.SeedAsync(
            context,
            Configuration(adminPassword: "initial-admin-password"),
            isDevelopment: false);

        var admin = await context.Users.SingleAsync(user => user.Username == "admin");
        admin.Active = false;
        await context.SaveChangesAsync();

        var activeToken = new RefreshToken
        {
            UserId = admin.Id,
            TokenHash = "active-bootstrap-admin-token",
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };
        var revokedToken = new RefreshToken
        {
            UserId = admin.Id,
            TokenHash = "revoked-bootstrap-admin-token",
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            RevokedAt = DateTime.UtcNow.AddHours(-1)
        };
        context.RefreshTokens.AddRange(activeToken, revokedToken);
        await context.SaveChangesAsync();
        var previousRevokedAt = revokedToken.RevokedAt;

        var logger = new ListLogger();
        await DbInitializer.SeedAsync(
            context,
            Configuration(adminPassword: "bootstrap-restored-password-123"),
            isDevelopment: false,
            logger);

        context.ChangeTracker.Clear();
        var updatedActiveToken = await context.RefreshTokens
            .SingleAsync(token => token.TokenHash == "active-bootstrap-admin-token");
        var updatedRevokedToken = await context.RefreshTokens
            .SingleAsync(token => token.TokenHash == "revoked-bootstrap-admin-token");

        Assert.NotNull(updatedActiveToken.RevokedAt);
        Assert.Equal(previousRevokedAt, updatedRevokedToken.RevokedAt);
        Assert.Contains(logger.Messages, message =>
            message.Contains("existing admin account was restored", StringComparison.Ordinal) &&
            message.Contains("refresh tokens", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Messages, message =>
            message.Contains("bootstrap-restored-password-123", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NoActiveAdminAndEmptyAdminPassword_SeedThrows()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        await DbInitializer.SeedAsync(
            context,
            Configuration(adminPassword: "initial-admin-password"),
            isDevelopment: false);

        var admin = await context.Users.SingleAsync(user => user.Username == "admin");
        admin.Active = false;
        await context.SaveChangesAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DbInitializer.SeedAsync(
                context,
                Configuration(adminPassword: ""),
                isDevelopment: false));

        Assert.Contains("ADMIN_PASSWORD", exception.Message);
    }

    [Fact]
    public async Task ActiveAdminExists_EmptyAdminPassword_SeedSucceeds()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        await DbInitializer.SeedAsync(
            context,
            Configuration(adminPassword: "initial-admin-password"),
            isDevelopment: false);
        var originalHash = (await context.Users.SingleAsync(user => user.Username == "admin")).Password;

        await DbInitializer.SeedAsync(
            context,
            Configuration(adminPassword: ""),
            isDevelopment: false);

        context.ChangeTracker.Clear();
        var admin = await context.Users.SingleAsync(user => user.Username == "admin");
        Assert.True(admin.Active);
        Assert.Equal(originalHash, admin.Password);
    }

    private static async Task AddAdminAsync(MtsDbContext context, string username)
    {
        var adminRole = await context.Roles.SingleAsync(role => role.Name == "Admin");
        var user = new User
        {
            Username = username,
            Active = true,
            AuthSource = "DB"
        };
        user.Password = BCrypt.Net.BCrypt.HashPassword("other-admin-password");
        user.Roles.Add(adminRole);
        context.Users.Add(user);
        await context.SaveChangesAsync();
    }

    private static IConfiguration Configuration(
        bool? demoDataEnabled = null,
        bool? demoUserEnabled = null,
        string adminPassword = "test-admin-password-123",
        bool adminPasswordReset = false)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ADMIN_PASSWORD"] = adminPassword,
            ["ADMIN_PASSWORD_RESET"] = adminPasswordReset.ToString().ToLowerInvariant(),
            ["DEMO_USER_ENABLED"] = demoUserEnabled?.ToString().ToLowerInvariant(),
            ["DEMO_DATA_ENABLED"] = demoDataEnabled?.ToString().ToLowerInvariant()
        };
        return new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
    }

    private static async Task<(int Chemicals, int Addresses, int Batches, int Units, int Tasks)> CountsAsync(MtsDbContext context) =>
        (await context.Chemicals.CountAsync(), await context.Addresses.CountAsync(),
            await context.Batches.CountAsync(), await context.Units.CountAsync(),
            await context.RelocationTasks.CountAsync());

    private static Mock<IHubContext<TraceabilityHub>> CreateHub()
    {
        var proxy = new Mock<IClientProxy>();
        var clients = new Mock<IHubClients>();
        clients.Setup(value => value.All).Returns(proxy.Object);
        proxy.Setup(value => value.SendCoreAsync(
                It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var hub = new Mock<IHubContext<TraceabilityHub>>();
        hub.Setup(value => value.Clients).Returns(clients.Object);
        return hub;
    }

    private sealed class ListLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
