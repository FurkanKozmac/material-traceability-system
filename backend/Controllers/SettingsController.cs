using backend.Common.Enums;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/settings")]
public sealed class SettingsController(ISettingsService settingsService) : ControllerBase
{
    [HttpGet("barcode-format")]
    public async Task<IActionResult> GetBarcodeFormat(CancellationToken ct) =>
        Ok(new { format = (await settingsService.GetBarcodeFormatAsync(ct)).ToString() });

    [HttpPut("barcode-format")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateBarcodeFormat([FromBody] UpdateBarcodeFormatRequest request, CancellationToken ct)
    {
        if (!Enum.TryParse<BarcodeFormat>(request.Format, true, out var format) || !Enum.IsDefined(format))
            return BadRequest(new { message = "Format QrCode veya Code128 olmalıdır." });
        var updated = await settingsService.UpdateBarcodeFormatAsync(format, ct);
        return Ok(new { format = updated.ToString() });
    }
}

public sealed record UpdateBarcodeFormatRequest(string Format);
