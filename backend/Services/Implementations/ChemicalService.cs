using backend.Common.Enums;
using backend.Data;
using backend.DTOs;
using backend.Entities;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Implementations;

public class ChemicalService(MtsDbContext context) : IChemicalService
{
    public async Task<List<ChemicalResponse>> GetAllAsync(CancellationToken ct = default)
    {
        return await context.Chemicals
            .AsNoTracking()
            .Select(c => new ChemicalResponse(
                c.Id,
                c.Name,
                c.ChemicalCode,
                c.Description,
                c.MsdsUrl,
                c.Stopped,
                c.StorageType
            ))
            .Take(50)
            .ToListAsync(ct);
    }

    public async Task<ChemicalResponse?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return await context.Chemicals
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new ChemicalResponse(
                c.Id,
                c.Name,
                c.ChemicalCode,
                c.Description,
                c.MsdsUrl,
                c.Stopped,
                c.StorageType
            ))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ChemicalResponse> CreateAsync(CreateChemicalRequest request, CancellationToken ct = default)
    {
        if (!Enum.TryParse<StorageType>(request.StorageType, true, out var storageType))
            throw new InvalidOperationException("Geçersiz depolama türü!");

        var exists = await context.Chemicals
            .AnyAsync(c => c.ChemicalCode == request.ChemicalCode, ct);

        if (exists)
        {
            throw new InvalidOperationException("Chemical already exists!");
        }

        var chemical = new Chemical
        {
            Name = request.Name,
            ChemicalCode = request.ChemicalCode,
            Description = request.Description,
            MsdsUrl = request.MsdsUrl,
            Stopped = false,
            StorageType = storageType.ToString()
        };

        context.Chemicals.Add(chemical);
        await context.SaveChangesAsync(ct);

        return new ChemicalResponse(chemical.Id,
            chemical.Name,
            chemical.ChemicalCode,
            chemical.Description,
            chemical.MsdsUrl,
            chemical.Stopped,
            chemical.StorageType);
    }

    public async Task<bool> UpdateAsync(long id, UpdateChemicalRequest request, CancellationToken ct = default)
    {
        if (!Enum.TryParse<StorageType>(request.StorageType, true, out var storageType))
            throw new InvalidOperationException("Geçersiz depolama türü!");

        var chemical = await context.Chemicals.FindAsync([id], ct);
        if (chemical is null) return false;

        chemical.Name = request.Name;
        chemical.Description = request.Description;
        chemical.MsdsUrl = request.MsdsUrl;
        chemical.Stopped = request.Stopped;
        chemical.StorageType = storageType.ToString();

        await context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
    {
        var chemical = await context.Chemicals.FindAsync([id], ct);
        if (chemical is null) return false;

        context.Chemicals.Remove(chemical);
        await context.SaveChangesAsync(ct);
        return true;
    }
}
