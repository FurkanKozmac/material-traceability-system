using backend.DTOs;

namespace backend.Services.Interfaces;

public interface IChemicalExcelService
{
    Task<byte[]> CreateTemplateAsync(CancellationToken ct = default);
    Task<BulkImportResultDto> ImportAsync(Stream stream, CancellationToken ct = default);
}
