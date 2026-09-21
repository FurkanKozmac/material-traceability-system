using backend.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController(MtsDbContext context) : ControllerBase
{
    [HttpGet("aggregate")]
    public async Task<IActionResult> GetAggregate(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var usableInStock = await context.Units.CountAsync(
            u => (u.Status == "InStock" || u.Status == "AVAILABLE") &&
                 (u.Batch.ExpirationDate == null || u.Batch.ExpirationDate >= now), ct);

        var blockedExpired = await context.Units.CountAsync(
            u => (u.Status == "InStock" || u.Status == "AVAILABLE") &&
                 u.Batch.ExpirationDate.HasValue &&
                 u.Batch.ExpirationDate.Value < now, ct);

        var consumed = await context.Units.CountAsync(u => u.Status == "Depleted", ct);
        var totalUnits = usableInStock + blockedExpired + consumed;

        return Ok(new
        {
            totalUnits,
            availableInStock = usableInStock,
            totalConsumed = consumed,
            pendingReuse = 0,
            blockedExpired
        });
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPending(CancellationToken ct)
    {
        var pendingTasksCount = await context.RelocationTasks
            .CountAsync(t => t.Status == "Pending", ct);

        return Ok(new { count = pendingTasksCount });
    }

    [HttpGet("recent-consumption")]
    public async Task<IActionResult> GetRecentConsumption(CancellationToken ct)
    {
        var recent = await context.Units
            .AsNoTracking()
            .Where(u => u.ConsumedAt != null)
            .OrderByDescending(u => u.ConsumedAt)
            .Take(10)
            .Select(u => new
            {
                time = u.ConsumedAt,
                action = "CONSUMED",
                barcode = u.Barcode,
                chemical = u.Batch.Chemical.Name,
                operatorName = "System Operator"
            })
            .ToListAsync(ct);

        return Ok(recent);
    }
}
