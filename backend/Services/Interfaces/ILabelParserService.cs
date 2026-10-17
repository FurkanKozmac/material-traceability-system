using backend.DTOs;

namespace backend.Services.Interfaces;

public interface ILabelParserService
{
    Task<BatchLabelScanResultDto> ParseAsync(string rawText, CancellationToken ct = default);
}
