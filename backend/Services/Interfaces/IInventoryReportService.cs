namespace backend.Services.Interfaces;

public interface IInventoryReportService
{
    Task<byte[]> ExportAuditReportAsync(CancellationToken ct = default);
}
