using backend.DTOs;

namespace backend.Services.Interfaces;

public interface IChemicalService
{
    Task<List<ChemicalResponse>> GetAllAsync(CancellationToken ct = default);
    Task<ChemicalResponse?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<ChemicalResponse> CreateAsync(CreateChemicalRequest request, CancellationToken ct = default);
    Task<bool> UpdateAsync(long id, UpdateChemicalRequest request, CancellationToken ct = default);
    Task<bool> DeleteAsync(long id, CancellationToken ct = default);
}
