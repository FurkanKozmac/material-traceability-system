using backend.Data;
using backend.Entities;
using backend.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace backend.Tests;

public class DashboardAlertServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task NearExpiry_IncludesOnlyInStockBatchesWithinThirtyDays()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var (_, soon, _, _) = await TestData.AddUnitAsync(context, expirationDate: Now.AddDays(9.5));
        var (_, boundary, _, _) = await TestData.AddUnitAsync(context, expirationDate: Now.AddDays(30));
        await TestData.AddUnitAsync(context, expirationDate: Now.AddDays(31));
        await TestData.AddUnitAsync(context, expirationDate: Now.AddDays(-1));
        await TestData.AddUnitAsync(context, status: "Depleted", expirationDate: Now.AddDays(5));
        await TestData.AddUnitAsync(context, expirationDate: null);

        // Act
        var result = await CreateService(context).GetAlertsAsync();

        // Assert
        Assert.Equal(30, result.NearExpiryDays);
        Assert.Equal(2, result.NearExpiryTotal);
        Assert.Equal([soon.BatchNo, boundary.BatchNo], result.NearExpiry.Select(b => b.BatchNo).ToList());
        Assert.Equal(10, result.NearExpiry[0].DaysLeft);
        Assert.Equal(1, result.NearExpiry[0].ActiveUnits);
    }

    [Fact]
    public async Task LowStock_UsesActiveUnitsBelowThreshold_IncludingExpiredInStockUnits()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var (atThreshold, _, _, _) = await TestData.AddUnitAsync(context);
        var (belowThreshold, _, _, _) = await TestData.AddUnitAsync(context);
        var (expiredButStocked, _, _, _) = await TestData.AddUnitAsync(context, expirationDate: Now.AddDays(-10));
        var (outOfStock, _, _, _) = await TestData.AddUnitAsync(context, status: "Depleted");
        atThreshold.MinStockThreshold = 1;
        belowThreshold.MinStockThreshold = 2;
        expiredButStocked.MinStockThreshold = 1;
        outOfStock.MinStockThreshold = 1;
        await context.SaveChangesAsync();

        // Act
        var result = await CreateService(context).GetAlertsAsync();

        // Assert
        Assert.Equal(2, result.LowStockTotal);
        Assert.Equal([outOfStock.ChemicalCode, belowThreshold.ChemicalCode],
            result.LowStock.Select(c => c.ChemicalCode).ToList());
        Assert.Equal(0, result.LowStock[0].ActiveUnits);
        Assert.Equal(2, result.LowStock[1].MinStockThreshold);
    }

    [Fact]
    public async Task Lists_AreLimitedToTenItems_WithFullTotals()
    {
        // Arrange
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        for (var i = 0; i < 12; i++)
            await TestData.AddUnitAsync(context, expirationDate: Now.AddDays(1 + i));

        // Act
        var result = await CreateService(context).GetAlertsAsync();

        // Assert: every helper chemical keeps the default threshold (10) with one unit, so all are low.
        Assert.Equal(12, result.NearExpiryTotal);
        Assert.Equal(DashboardAlertService.MaxItems, result.NearExpiry.Count);
        Assert.Equal(Enumerable.Range(1, 10).ToList(), result.NearExpiry.Select(b => b.DaysLeft).ToList());
        Assert.Equal(12, result.LowStockTotal);
        Assert.Equal(DashboardAlertService.MaxItems, result.LowStock.Count);
    }

    private static DashboardAlertService CreateService(MtsDbContext context) =>
        new(context, new FixedTimeProvider(Now));
}

[Collection(PostgresTestCollection.Name)]
[Trait("Category", "Testcontainers")]
public class DashboardAlertServicePostgresTests(PostgresTestDatabase database)
{
    [Fact]
    public async Task DemoSeed_ProducesBalancedAlerts()
    {
        // Use a dedicated database so rows from other tests in the shared container do not affect the lists.
        var connectionString = new NpgsqlConnectionStringBuilder(database.ConnectionString)
        {
            Database = $"dashboard_{Guid.NewGuid():N}"
        }.ConnectionString;
        await using var context = new MtsDbContext(
            new DbContextOptionsBuilder<MtsDbContext>().UseNpgsql(connectionString).Options);
        await context.Database.MigrateAsync();
        await DemoDataSeeder.SeedAsync(context);

        var result = await new DashboardAlertService(context, TimeProvider.System).GetAlertsAsync();

        Assert.Equal(["DEMO-BAT-002"], result.NearExpiry.Select(b => b.BatchNo).ToList());
        Assert.Equal(3, result.LowStockTotal);
        Assert.Equal(["DEMO-CHM-04", "DEMO-CHM-03", "DEMO-CHM-10"], result.LowStock.Select(c => c.ChemicalCode).ToList());

        await context.Database.EnsureDeletedAsync();
    }
}

internal sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => new(utcNow);
}
