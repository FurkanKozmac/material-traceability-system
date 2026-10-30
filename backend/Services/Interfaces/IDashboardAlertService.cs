using backend.DTOs;

namespace backend.Services.Interfaces;

public interface IDashboardAlertService
{
    Task<DashboardAlertsResponse> GetAlertsAsync(CancellationToken ct = default);
}
