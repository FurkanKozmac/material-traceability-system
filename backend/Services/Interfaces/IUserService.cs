using backend.DTOs;

namespace backend.Services.Interfaces;

public interface IUserService
{
    Task<List<UserResponse>> GetAllAsync(CancellationToken ct = default);
    Task<UserResponse> GetByIdAsync(long id, CancellationToken ct = default);
    Task<UserResponse> CreateAsync(CreateUserRequest request, CancellationToken ct = default);
    Task<UserResponse> UpdateRoleAsync(long id, UpdateUserRoleRequest request, CancellationToken ct = default);
    Task<UserResponse> UpdateActiveAsync(
        long id, UpdateUserActiveRequest request, long currentUserId, CancellationToken ct = default);
    Task ResetPasswordAsync(long id, ResetUserPasswordRequest request, CancellationToken ct = default);
    Task DeleteAsync(long id, long currentUserId, CancellationToken ct = default);
}
