namespace backend.Services.Interfaces;

public interface IOcrService
{
    Task<string> ExtractTextAsync(Stream image, string contentType, CancellationToken ct = default);
}
