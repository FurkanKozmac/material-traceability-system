using backend.Data;
using backend.DTOs;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace backend.Controllers;

[ApiController]
[Route("api/batches")]
public class BatchesController(IBatchService batchService, MtsDbContext context) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<BatchResponse>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] int? page,
        [FromQuery] int? size,
        CancellationToken ct) =>
        Ok(await batchService.GetAllAsync(search, page ?? 1, size ?? 20, ct));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<BatchResponse>> GetById(long id, CancellationToken ct)
    {
        var batch = await batchService.GetByIdAsync(id, ct);
        return batch is null ? NotFound("Parti bulunamadı.") : Ok(batch);
    }

    [HttpGet("{id:long}/units")]
    public async Task<IActionResult> GetBatchUnits(long id, CancellationToken ct)
    {
        var units = await context.Units
            .AsNoTracking()
            .Where(u => u.BatchId == id)
            .Select(u => new
            {
                u.Id,
                u.Barcode,
                u.Status,
                u.Batch.Chemical.Name,
                u.Batch.ExpirationDate
            })
            .ToListAsync(ct);

        return Ok(units);
    }

    [HttpGet("barcode/{barcode}")]
    public async Task<IActionResult> GetByBarcode(string barcode, CancellationToken ct)
    {
        const string prefix = "BAT-";
        if (!barcode.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return BadRequest("Geçersiz batch barkodu.");

        var batchNo = barcode[prefix.Length..];
        var batch = await context.Batches
            .AsNoTracking()
            .Where(b => b.BatchNo == batchNo)
            .Select(b => new
            {
                b.Id,
                b.BatchNo,
                BatchBarcode = prefix + b.BatchNo,
                b.ChemicalId,
                ChemicalName = b.Chemical.Name,
                ChemicalCode = b.Chemical.ChemicalCode,
                b.ExpirationDate,
                TotalUnits = b.Units.Count,
                AvailableUnits = b.Units.Count(u => u.Status == "InStock" || u.Status == "AVAILABLE"),
                ConsumedUnits = b.Units.Count(u => u.Status == "Depleted")
            })
            .FirstOrDefaultAsync(ct);

        if (batch is null)
            return NotFound("Batch bulunamadı.");

        var addressCodes = await context.Units
            .AsNoTracking()
            .Where(u => u.BatchId == batch.Id && u.Address != null)
            .Select(u => u.Address!.Code)
            .Distinct()
            .OrderBy(code => code)
            .ToListAsync(ct);

        return Ok(new
        {
            batch.Id,
            batch.BatchNo,
            batch.BatchBarcode,
            batch.ChemicalId,
            batch.ChemicalName,
            batch.ChemicalCode,
            batch.ExpirationDate,
            batch.TotalUnits,
            batch.AvailableUnits,
            batch.ConsumedUnits,
            AddressCodes = addressCodes
        });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<BatchResponse>> Create(CreateBatchRequest request, CancellationToken ct)
    {
        try
        {
            var batch = await batchService.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = batch.Id }, batch);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
