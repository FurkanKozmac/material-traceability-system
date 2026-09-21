using backend.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace backend.Controllers;

[ApiController]
[Route("api")]
public class CompatibilityController(MtsDbContext context) : ControllerBase
{
    [HttpGet("warehouses")]
    public IActionResult GetWarehouses() =>
        Ok(new[] { new { id = 1, name = "Ana Kimyasal Deposu", code = "WH-MAIN" } });

    [HttpGet("shops")]
    public IActionResult GetShops() =>
        Ok(new[] { new { id = 1, shopCode = "SHOP-01", shopName = "Boya / Kaplama Atölyesi" } });

    [HttpGet("types")]
    public IActionResult GetTypes() =>
        Ok(new[] { new { id = 1, code = "SHELF", description = "Standart Depo Rafı" } });

    [HttpGet("sectors")]
    public IActionResult GetSectors() =>
        Ok(new[] { new { id = 1, code = "SEC-A", description = "A Sektörü (Yanıcı Olmayan)" } });

    [HttpGet("stock-limits")]
    public IActionResult GetStockLimits() => Ok(Array.Empty<object>());

    [HttpGet("status")]
    public IActionResult GetStatus() => Ok(new { status = "HEALTHY", timestamp = DateTime.UtcNow });

    [HttpGet("health")]
    [AllowAnonymous]
    public IActionResult GetHealth() => Ok(new { status = "HEALTHY", timestamp = DateTime.UtcNow });

    [HttpGet("material-parties")]
    public async Task<IActionResult> GetMaterialParties(CancellationToken ct)
    {
        var batches = await context.Batches
            .AsNoTracking()
            .Select(b => new { b.Id, b.BatchNo, Chemical = b.Chemical.Name })
            .ToListAsync(ct);

        return Ok(batches);
    }
}
