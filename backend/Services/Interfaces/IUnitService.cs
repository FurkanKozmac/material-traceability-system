using backend.DTOs;

namespace backend.Services.Interfaces;

public interface IUnitService
{
    Task<UnitResponse?> GetByBarcodeAsync(string barcode, CancellationToken ct = default);
    Task<List<UnitResponse>> SearchAsync(string? search, long? addressId, int size = 100, CancellationToken ct = default);
    Task<List<UnitResponse>> GetByAddressIdAsync(long addressId, CancellationToken ct = default);
    Task<UnitResponse> CreateAsync(CreateUnitRequest request, CancellationToken ct = default);
    Task<UnitResponse> ConsumeAsync(string barcode, CancellationToken ct = default);
}
