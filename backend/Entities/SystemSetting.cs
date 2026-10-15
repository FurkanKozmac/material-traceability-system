using backend.Common.Enums;

namespace backend.Entities;

public class SystemSetting
{
    public int Id { get; set; } = 1;
    public BarcodeFormat DefaultBarcodeFormat { get; set; } = BarcodeFormat.QrCode;
    public string AlertEmailRecipient { get; set; } = "alerts@mts.local";
    public bool EnableLowStockAlerts { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
