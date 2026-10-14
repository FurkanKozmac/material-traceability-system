using backend.Services.Implementations.Barcodes;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/labels")]
public sealed class LabelsController(ISettingsService settingsService, BarcodeGeneratorFactory factory) : ControllerBase
{
    [HttpGet("preview")]
    public async Task<IActionResult> Preview([FromQuery] string payload, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(payload)) return BadRequest("Payload gereklidir.");
        if (payload.Length > 256) return BadRequest("Payload en fazla 256 karakter olabilir.");
        var format = await settingsService.GetBarcodeFormatAsync(ct);
        var image = factory.GetStrategy(format).GenerateImage(payload);
        return File(image, "image/png");
    }
}
