using backend.Common.Enums;

namespace backend.Services.Interfaces;

public interface ISettingsService
{
    Task<BarcodeFormat> GetBarcodeFormatAsync(CancellationToken ct = default);
    Task<BarcodeFormat> UpdateBarcodeFormatAsync(BarcodeFormat format, CancellationToken ct = default);
    Task<NotificationSettingsResponse> GetNotificationSettingsAsync(CancellationToken ct = default);
    Task<NotificationSettingsResponse> UpdateNotificationSettingsAsync(string recipient, bool enabled, CancellationToken ct = default);
}

public sealed record NotificationSettingsResponse(string AlertEmailRecipient, bool EnableLowStockAlerts);
