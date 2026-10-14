using backend.Common.Enums;
using backend.Services.Interfaces;

namespace backend.Services.Implementations.Barcodes;

public sealed class BarcodeGeneratorFactory(IEnumerable<IBarcodeGeneratorStrategy> strategies)
{
    public IBarcodeGeneratorStrategy GetStrategy(BarcodeFormat format) =>
        strategies.FirstOrDefault(strategy => strategy.Format == format)
        ?? throw new InvalidOperationException($"{format} barkod biçimi için üretici kaydı bulunamadı.");
}
