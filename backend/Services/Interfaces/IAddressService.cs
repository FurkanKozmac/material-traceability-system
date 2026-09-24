using backend.DTOs;

namespace backend.Services.Interfaces;

public interface IAddressService
{
    Task<PagedResult<AddressResponse>> GetAllAsync(int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<AddressResponse?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<AddressResponse> CreateAsync(CreateAddressRequest request, CancellationToken ct = default);
    Task<bool> UpdateStorageTypeAsync(long id, string storageType, CancellationToken ct = default);
    Task<bool> UpdateCapacityAsync(long id, int maxCapacity, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}
