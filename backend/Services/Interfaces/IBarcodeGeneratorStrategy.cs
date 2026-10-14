using backend.Common.Enums;

namespace backend.Services.Interfaces;

public interface IBarcodeGeneratorStrategy
{
    BarcodeFormat Format { get; }
    byte[] GenerateImage(string payload);
}
