using backend.Data;
using backend.Entities;
using backend.Hubs;
using backend.Services.Implementations;
using backend.Services.Implementations.Notifications;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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

    private static IConfiguration Configuration(bool? demoDataEnabled = null, bool? demoUserEnabled = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ADMIN_PASSWORD"] = "test-admin-password-123",
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
}
