using backend.DTOs;

namespace backend.Services.Interfaces;

public interface IBatchService
{
    Task<List<BatchResponse>> GetAllAsync(string? search = null, CancellationToken ct = default);
    Task<BatchResponse?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<BatchResponse> CreateAsync(CreateBatchRequest request, CancellationToken ct = default);
}
