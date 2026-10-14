using backend.Common.Enums;
using backend.Services.Interfaces;
using QRCoder;

namespace backend.Services.Implementations.Barcodes;

public sealed class QrCodeGeneratorStrategy : IBarcodeGeneratorStrategy
{
    public BarcodeFormat Format => BarcodeFormat.QrCode;

    public byte[] GenerateImage(string payload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data);
        return png.GetGraphic(10);
    }
}
