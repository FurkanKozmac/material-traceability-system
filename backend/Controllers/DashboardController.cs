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

        var counts = await context.Units
            .GroupBy(_ => 1)
            .Select(group => new
            {
                UsableInStock = group.Count(u => (u.Status == "InStock" || u.Status == "AVAILABLE") &&
                    (u.Batch.ExpirationDate == null || u.Batch.ExpirationDate >= now)),
                BlockedExpired = group.Count(u => (u.Status == "InStock" || u.Status == "AVAILABLE") &&
                    u.Batch.ExpirationDate.HasValue && u.Batch.ExpirationDate.Value < now),
                Consumed = group.Count(u => u.Status == "Depleted")
            })
            .FirstOrDefaultAsync(ct);

        var usableInStock = counts?.UsableInStock ?? 0;
        var blockedExpired = counts?.BlockedExpired ?? 0;
        var consumed = counts?.Consumed ?? 0;
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
