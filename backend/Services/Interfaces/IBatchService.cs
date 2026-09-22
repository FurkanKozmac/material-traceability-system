using backend.DTOs;

namespace backend.Services.Interfaces;

public interface IBatchService
{
    Task<PagedResult<BatchResponse>> GetAllAsync(string? search = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<BatchResponse?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<BatchResponse> CreateAsync(CreateBatchRequest request, CancellationToken ct = default);
}
