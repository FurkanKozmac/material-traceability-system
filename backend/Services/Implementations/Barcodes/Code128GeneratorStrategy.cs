using backend.Common.Enums;
using backend.Services.Interfaces;
using BarcodeStandard;

namespace backend.Services.Implementations.Barcodes;

public sealed class Code128GeneratorStrategy : IBarcodeGeneratorStrategy
{
    public BarcodeFormat Format => BarcodeFormat.Code128;

    public byte[] GenerateImage(string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        var barcode = new Barcode { IncludeLabel = true };
        using var image = barcode.Encode(BarcodeStandard.Type.Code128, payload,
            SkiaSharp.SKColors.Black, SkiaSharp.SKColors.White, 900, 220);
        using var png = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }
}
