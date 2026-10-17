using backend.Data;
using backend.DTOs;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using backend.Common.Exceptions;

namespace backend.Controllers;

[ApiController]
[Route("api/batches")]
public class BatchesController(
    IBatchService batchService,
    MtsDbContext context,
    IOcrService ocrService,
    ILabelParserService labelParserService) : ControllerBase
{
    private const long MaxLabelImageBytes = 10 * 1024 * 1024;

    [HttpPost("scan-label")]
    [Authorize(Roles = "Admin,Operator")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxLabelImageBytes)]
    public async Task<ActionResult<BatchLabelScanResultDto>> ScanLabel(
        [FromForm] IFormFile? image,
        CancellationToken ct)
    {
        if (image is null || image.Length == 0)
            return BadRequest("Etiket görseli gereklidir.");
        if (image.Length > MaxLabelImageBytes)
            return StatusCode(StatusCodes.Status413PayloadTooLarge, "Görsel en fazla 10 MB olabilir.");
        if (image.ContentType is not ("image/jpeg" or "image/png" or "image/webp"))
            return BadRequest("Yalnızca JPEG, PNG veya WebP görselleri kabul edilir.");

        await using var stream = image.OpenReadStream();
        var rawText = await ocrService.ExtractTextAsync(stream, image.ContentType, ct);
        if (string.IsNullOrWhiteSpace(rawText))
            throw new OcrServiceUnavailableException("Görselden metin okunamadı.");

        return Ok(await labelParserService.ParseAsync(rawText, ct));
    }

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
