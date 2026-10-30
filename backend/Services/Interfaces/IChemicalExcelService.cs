using backend.DTOs;

namespace backend.Services.Interfaces;

public interface IChemicalExcelService
{
    Task<byte[]> CreateTemplateAsync(CancellationToken ct = default);
    Task<BulkImportResultDto> ImportAsync(Stream stream, bool dryRun = false, CancellationToken ct = default);
}
