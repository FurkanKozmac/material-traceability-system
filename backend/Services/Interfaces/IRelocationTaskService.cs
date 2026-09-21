using backend.DTOs;

namespace backend.Services.Interfaces;

public interface IRelocationTaskService
{
    Task<List<RelocationTaskResponse>> GetAllAsync(CancellationToken ct = default);
    Task<List<RelocationTargetResponse>> GetEligibleTargetsAsync(long unitId, CancellationToken ct = default);
    Task<RelocationTaskResponse> CreateTaskAsync(CreateRelocationTaskRequest request, CancellationToken ct = default);
    Task<bool> CompleteTaskAsync(long taskId, CompleteRelocationTaskRequest request, long? completedByUserId, CancellationToken ct = default);
    Task<bool> CancelTaskAsync(long taskId, CancellationToken ct = default);
}
