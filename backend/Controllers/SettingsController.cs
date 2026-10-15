using backend.Common.Enums;
using System.ComponentModel.DataAnnotations;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/settings")]
public sealed class SettingsController(ISettingsService settingsService, IEmailService emailService,
    ILogger<SettingsController> logger) : ControllerBase
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
    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications(CancellationToken ct) =>
        Ok(await settingsService.GetNotificationSettingsAsync(ct));

    [HttpPut("notifications")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateNotifications([FromBody] UpdateNotificationSettingsRequest request, CancellationToken ct)
    {
        if (!new EmailAddressAttribute().IsValid(request.AlertEmailRecipient))
            return BadRequest(new { message = "Geçerli bir bildirim e-posta adresi girilmelidir." });
        return Ok(await settingsService.UpdateNotificationSettingsAsync(request.AlertEmailRecipient, request.EnableLowStockAlerts, ct));
    }

    [HttpPost("test-alert-email")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SendTestAlertEmail(CancellationToken ct)
    {
        var settings = await settingsService.GetNotificationSettingsAsync(ct);
        try
        {
            await emailService.SendEmailAsync(settings.AlertEmailRecipient, "MTS test e-postası",
                "<div style='font-family:Arial,sans-serif;padding:24px'><h2>MTS e-posta testi başarılı</h2><p>Kritik stok bildirim altyapısı çalışıyor.</p></div>", ct);
            return Ok(new { message = "Test e-postası gönderildi." });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Test e-postası gönderilemedi; SMTP bağlantısını kontrol edin.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "E-posta sunucusuna ulaşılamadı. SMTP_HOST ve SMTP_PORT ayarlarını kontrol edin." });
        }
    }
}

public sealed record UpdateNotificationSettingsRequest([Required, EmailAddress] string AlertEmailRecipient, bool EnableLowStockAlerts);
public sealed record UpdateBarcodeFormatRequest(string Format);
