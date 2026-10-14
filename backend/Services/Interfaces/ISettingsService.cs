using backend.Common.Enums;

namespace backend.Services.Interfaces;

public interface ISettingsService
{
    Task<BarcodeFormat> GetBarcodeFormatAsync(CancellationToken ct = default);
    Task<BarcodeFormat> UpdateBarcodeFormatAsync(BarcodeFormat format, CancellationToken ct = default);
}
