using backend.Common.Exceptions;
using backend.Data;
using backend.DTOs;
using backend.Entities;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Implementations;

public class UnitService(MtsDbContext context) : IUnitService
{
    public async Task<List<UnitResponse>> SearchAsync(
        string? search, long? addressId, int size = 100, CancellationToken ct = default)
    {
        var query = context.Units.AsNoTracking().AsQueryable();

        if (addressId.HasValue)
            query = query.Where(u => u.AddressId == addressId.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(u =>
                EF.Functions.ILike(u.Barcode, term) ||
                EF.Functions.ILike(u.Batch.BatchNo, term) ||
                EF.Functions.ILike(u.Batch.Chemical.Name, term) ||
                EF.Functions.ILike(u.Batch.Chemical.ChemicalCode, term) ||
                (u.Address != null && EF.Functions.ILike(u.Address.Code, term)));
        }

        return await query
            .OrderByDescending(u => u.Id)
            .Take(Math.Clamp(size, 1, 500))
            .Select(u => new UnitResponse(
                u.Id,
                u.Barcode,
                u.Status == "AVAILABLE" ? "InStock" : u.Status,
                u.AddressId,
                u.Address != null ? u.Address.Code : null,
                u.BatchId,
                u.Batch.BatchNo,
                u.Batch.Chemical.Name,
                u.Batch.Chemical.ChemicalCode,
                u.Batch.ExpirationDate,
                u.Batch.ExpirationDate.HasValue && u.Batch.ExpirationDate.Value < DateTime.UtcNow))
            .ToListAsync(ct);
    }

    public async Task<UnitResponse?> GetByBarcodeAsync(string barcode, CancellationToken ct = default)
    {
        return await context.Units
            .AsNoTracking()
            .Where(u => u.Barcode == barcode)
            .Select(u => new UnitResponse(
                u.Id,
                u.Barcode,
                u.Status == "AVAILABLE" ? "InStock" : u.Status,
                u.AddressId,
                u.Address != null ? u.Address.Code : null,
                u.BatchId,
                u.Batch.BatchNo,
                u.Batch.Chemical.Name,
                u.Batch.Chemical.ChemicalCode,
                u.Batch.ExpirationDate,
                u.Batch.ExpirationDate.HasValue && u.Batch.ExpirationDate.Value < DateTime.UtcNow
            ))
            .FirstOrDefaultAsync(ct);
    }
    
    public async Task<List<UnitResponse>> GetByAddressIdAsync(long addressId, CancellationToken ct = default)
    {
        return await context.Units
            .AsNoTracking()
            .Where(u => u.AddressId == addressId)
            .Select(u => new UnitResponse(
                u.Id,
                u.Barcode,
                u.Status == "AVAILABLE" ? "InStock" : u.Status,
                u.AddressId,
                u.Address != null ? u.Address.Code : null,
                u.BatchId,
                u.Batch.BatchNo,
                u.Batch.Chemical.Name,
                u.Batch.Chemical.ChemicalCode,
                u.Batch.ExpirationDate,
                u.Batch.ExpirationDate.HasValue && u.Batch.ExpirationDate.Value < DateTime.UtcNow
            ))
            .ToListAsync(ct);
    }
    
    public async Task<UnitResponse> CreateAsync(CreateUnitRequest request, CancellationToken ct = default)
    {
        var exists = await context.Units.AnyAsync(u => u.Barcode == request.Barcode, ct);
        if (exists)
            throw new InvalidOperationException($"'{request.Barcode}' barkodu zaten kayıtlı!");
        
        var batchExists = await context.Batches.AnyAsync(b => b.Id == request.BatchId, ct);
        if (!batchExists)
            throw new InvalidOperationException("Belirtilen parti (Batch) bulunamadı!");

        var unit = new Unit
        {
            Barcode = request.Barcode,
            BatchId = request.BatchId,
            AddressId = request.AddressId,
            Status = "InStock",
            Version = 1
        };

        context.Units.Add(unit);
        await context.SaveChangesAsync(ct);

        return (await GetByBarcodeAsync(unit.Barcode, ct))!;
    }

    public async Task<UnitResponse> ConsumeAsync(string barcode, CancellationToken ct = default)
    {
        var unit = await context.Units
            .Include(u => u.Batch).ThenInclude(b => b.Chemical)
            .Include(u => u.Address)
            .FirstOrDefaultAsync(u => u.Barcode == barcode, ct);

        if (unit is null)
            throw new InvalidOperationException($"'{barcode}' barkodlu varil bulunamadı!");

        if (unit.Status != "InStock" && unit.Status != "AVAILABLE")
            throw new BusinessRuleException("Yalnızca stokta bulunan bir varil tüketime verilebilir!");

        if (unit.Batch.ExpirationDate.HasValue && unit.Batch.ExpirationDate.Value < DateTime.UtcNow)
            throw new BusinessRuleException(
                "DİKKAT: Son kullanma tarihi geçmiş malzeme tüketime verilemez!");

        if (unit.Batch.Chemical.Stopped)
            throw new BusinessRuleException("Durdurulmuş bir kimyasala ait varil tüketime verilemez!");

        var pendingTasks = await context.RelocationTasks
            .Where(t => t.UnitId == unit.Id && t.Status == "Pending")
            .ToListAsync(ct);

        foreach (var task in pendingTasks)
        {
            task.Status = "Cancelled";
            task.CompletedAt = DateTime.UtcNow;
        }

        unit.Status = "Depleted";
        unit.ConsumedAt = DateTime.UtcNow;
        unit.AddressId = null;
        unit.Version++;

        await context.SaveChangesAsync(ct);
        return (await GetByBarcodeAsync(unit.Barcode, ct))!;
    }
}
