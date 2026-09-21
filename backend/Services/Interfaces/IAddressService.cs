using backend.DTOs;

namespace backend.Services.Interfaces;

public interface IAddressService
{
    Task<List<AddressResponse>> GetAllAsync(CancellationToken ct = default);
    Task<AddressResponse?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<AddressResponse> CreateAsync(CreateAddressRequest request, CancellationToken ct = default);
    Task<bool> UpdateStorageTypeAsync(long id, string storageType, CancellationToken ct = default);
}
