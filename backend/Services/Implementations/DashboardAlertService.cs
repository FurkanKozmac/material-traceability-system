using backend.Data;
using backend.DTOs;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Implementations;

public class DashboardAlertService(MtsDbContext context, TimeProvider timeProvider) : IDashboardAlertService
{
    public const int NearExpiryDays = 30;
    public const int MaxItems = 10;

    public async Task<DashboardAlertsResponse> GetAlertsAsync(CancellationToken ct = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var horizon = now.AddDays(NearExpiryDays);

        // Filters are applied to the entity queries before projecting so EF can translate them.
        var nearExpiryQuery = context.Batches
            .AsNoTracking()
            .Where(b => b.ExpirationDate != null && b.ExpirationDate >= now && b.ExpirationDate <= horizon &&
                b.Units.Any(u => u.Status == "InStock" || u.Status == "AVAILABLE"));
        var nearExpiryTotal = await nearExpiryQuery.CountAsync(ct);
        var nearExpiryRows = await nearExpiryQuery
            .OrderBy(b => b.ExpirationDate)
            .ThenBy(b => b.BatchNo)
            .Take(MaxItems)
            .Select(b => new
            {
                b.Id,
                b.BatchNo,
                ChemicalName = b.Chemical.Name,
                ExpirationDate = b.ExpirationDate!.Value,
                ActiveUnits = b.Units.Count(u => u.Status == "InStock" || u.Status == "AVAILABLE")
            })
            .ToListAsync(ct);

        // Same count as the low-stock email trigger: expired units that are still in stock are included.
        var lowStockQuery = context.Chemicals
            .AsNoTracking()
            .Where(c => c.Batches.SelectMany(b => b.Units)
                .Count(u => u.Status == "InStock" || u.Status == "AVAILABLE") < c.MinStockThreshold);
        var lowStockTotal = await lowStockQuery.CountAsync(ct);
        var lowStock = await lowStockQuery
            .Select(c => new
            {
                c.Id,
                c.ChemicalCode,
                c.Name,
                ActiveUnits = c.Batches.SelectMany(b => b.Units)
                    .Count(u => u.Status == "InStock" || u.Status == "AVAILABLE"),
                c.MinStockThreshold
            })
            .OrderBy(c => c.ActiveUnits)
            .ThenBy(c => c.ChemicalCode)
            .Take(MaxItems)
            .ToListAsync(ct);

        return new DashboardAlertsResponse(
            NearExpiryDays,
            nearExpiryTotal,
            nearExpiryRows.Select(b => new NearExpiryBatchDto(b.Id, b.BatchNo, b.ChemicalName, b.ExpirationDate,
                (int)Math.Ceiling((b.ExpirationDate - now).TotalDays), b.ActiveUnits)).ToList(),
            lowStockTotal,
            lowStock.Select(c => new LowStockChemicalDto(c.Id, c.ChemicalCode, c.Name, c.ActiveUnits,
                c.MinStockThreshold)).ToList());
    }
}
