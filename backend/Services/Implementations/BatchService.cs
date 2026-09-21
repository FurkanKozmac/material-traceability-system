using backend.Common.Exceptions;
using backend.Data;
using backend.DTOs;
using backend.Entities;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Implementations;

public class BatchService(MtsDbContext context) : IBatchService
{
    public async Task<List<BatchResponse>> GetAllAsync(string? search = null, CancellationToken ct = default)
    {
        var query = context.Batches.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(b =>
                EF.Functions.ILike(b.BatchNo, term) ||
                EF.Functions.ILike(b.Chemical.Name, term) ||
                EF.Functions.ILike(b.Chemical.ChemicalCode, term) ||
                (b.Supplier != null && EF.Functions.ILike(b.Supplier, term)));
        }

        return await query
            .OrderByDescending(b => b.Id)
            .Select(b => new BatchResponse(
                b.Id, b.BatchNo, b.Supplier, b.InitialQuantity, b.ExpirationDate,
                b.ChemicalId, b.Chemical.Name, b.Units.Count))
            .ToListAsync(ct);
    }

    public async Task<BatchResponse?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return await context.Batches
            .AsNoTracking()
            .Where(b => b.Id == id)
            .Select(b => new BatchResponse(
                b.Id, b.BatchNo, b.Supplier, b.InitialQuantity, b.ExpirationDate,
                b.ChemicalId, b.Chemical.Name, b.Units.Count))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<BatchResponse> CreateAsync(CreateBatchRequest request, CancellationToken ct = default)
    {
        var chemical = await context.Chemicals
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == request.ChemicalId, ct);

        if (chemical is null)
            throw new InvalidOperationException("Belirtilen kimyasal bulunamadı!");

        if (chemical.Stopped)
            throw new BusinessRuleException("Durdurulmuş bir kimyasal için yeni parti açılamaz!");

        if (await context.Batches.AnyAsync(b => b.BatchNo == request.BatchNo, ct))
            throw new InvalidOperationException($"'{request.BatchNo}' parti numarası zaten mevcut!");

        var initialAddress = await context.Addresses
            .AsNoTracking()
            .Where(a => a.StorageType == chemical.StorageType)
            .Where(a => !a.MaxCapacity.HasValue ||
                a.Units.Count(u => u.Status == "InStock" || u.Status == "AVAILABLE") + request.InitialQuantity <= a.MaxCapacity.Value)
            .OrderBy(a => a.Id)
            .Select(a => new
            {
                a.Id,
                a.Code,
                a.MaxCapacity,
                Occupancy = a.Units.Count(u => u.Status == "InStock" || u.Status == "AVAILABLE")
            })
            .FirstOrDefaultAsync(ct);

        if (initialAddress is null)
            throw new BusinessRuleException(
                $"{chemical.StorageType} depolama türünde, partideki variller için yeterli kapasiteye sahip bir raf bulunamadı!");

        if (initialAddress.MaxCapacity.HasValue &&
            initialAddress.Occupancy + request.InitialQuantity > initialAddress.MaxCapacity.Value)
        {
            throw new BusinessRuleException(
                $"Kabul rafı ({initialAddress.Code}) parti miktarı için yeterli kapasiteye sahip değil!");
        }

        var batch = new Batch
        {
            BatchNo = request.BatchNo,
            Supplier = request.Supplier,
            InitialQuantity = request.InitialQuantity,
            ExpirationDate = request.ExpirationDate,
            ChemicalId = request.ChemicalId
        };

        for (var index = 1; index <= request.InitialQuantity; index++)
        {
            batch.Units.Add(new Unit
            {
                Barcode = $"BAR-{request.BatchNo}-{index:D3}",
                Status = "InStock",
                Version = 1,
                AddressId = initialAddress.Id
            });
        }

        context.Batches.Add(batch);
        await context.SaveChangesAsync(ct);
        return (await GetByIdAsync(batch.Id, ct))!;
    }
}
