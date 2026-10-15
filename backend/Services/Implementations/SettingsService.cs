using backend.Common.Enums;
using backend.Data;
using backend.Entities;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Implementations;

public sealed class SettingsService(MtsDbContext context) : ISettingsService
{
    public async Task<BarcodeFormat> GetBarcodeFormatAsync(CancellationToken ct = default)
    {
        var setting = await GetOrCreateAsync(ct);
        return setting.DefaultBarcodeFormat;
    }

    public async Task<BarcodeFormat> UpdateBarcodeFormatAsync(BarcodeFormat format, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(format)) throw new ArgumentOutOfRangeException(nameof(format));
        var setting = await GetOrCreateAsync(ct);
        setting.DefaultBarcodeFormat = format;
        setting.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
        return setting.DefaultBarcodeFormat;
    }

    public async Task<NotificationSettingsResponse> GetNotificationSettingsAsync(CancellationToken ct = default)
    {
        var setting = await GetOrCreateAsync(ct);
        return new NotificationSettingsResponse(setting.AlertEmailRecipient, setting.EnableLowStockAlerts);
    }

    public async Task<NotificationSettingsResponse> UpdateNotificationSettingsAsync(string recipient, bool enabled, CancellationToken ct = default)
    {
        var setting = await GetOrCreateAsync(ct);
        setting.AlertEmailRecipient = recipient.Trim();
        setting.EnableLowStockAlerts = enabled;
        setting.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(ct);
        return new NotificationSettingsResponse(setting.AlertEmailRecipient, setting.EnableLowStockAlerts);
    }

    private async Task<SystemSetting> GetOrCreateAsync(CancellationToken ct)
    {
        var setting = await context.SystemSettings.SingleOrDefaultAsync(s => s.Id == 1, ct);
        if (setting is not null) return setting;
        setting = new SystemSetting { Id = 1, DefaultBarcodeFormat = BarcodeFormat.QrCode, AlertEmailRecipient = "alerts@mts.local", EnableLowStockAlerts = true, UpdatedAt = DateTime.UtcNow };
        context.SystemSettings.Add(setting);
        await context.SaveChangesAsync(ct);
        return setting;
    }
}
