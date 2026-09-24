using backend.Common.Exceptions;
using backend.Data;
using backend.DTOs;
using backend.Entities;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace backend.Services.Implementations;

public class BatchService(MtsDbContext context) : IBatchService
{
    public async Task<PagedResult<BatchResponse>> GetAllAsync(string? search = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
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

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(b => b.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(b => new BatchResponse(
                b.Id, b.BatchNo, b.Supplier, b.InitialQuantity, b.ExpirationDate,
                b.ChemicalId, b.Chemical.Name,
                b.Units.Count(u => u.Status == "InStock" || u.Status == "AVAILABLE")))
            .ToListAsync(ct);
        return new PagedResult<BatchResponse>(items, totalCount, page, pageSize);
    }

    public async Task<BatchResponse?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return await context.Batches
            .AsNoTracking()
            .Where(b => b.Id == id)
            .Select(b => new BatchResponse(
                b.Id, b.BatchNo, b.Supplier, b.InitialQuantity, b.ExpirationDate,
                b.ChemicalId, b.Chemical.Name,
                b.Units.Count(u => u.Status == "InStock" || u.Status == "AVAILABLE")))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<BatchResponse> CreateAsync(CreateBatchRequest request, CancellationToken ct = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
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
            .Where(a =>
                a.Units.Count(u => u.Status == "InStock" || u.Status == "AVAILABLE") + request.InitialQuantity <= a.MaxCapacity)
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
                $"{chemical.StorageType} depolama türünde, partideki variller için yeterli kapasiteye sahip bir raf bulunamadı!",
                "INSUFFICIENT_RACK_CAPACITY",
                new { storageType = chemical.StorageType });

        var lockedAddress = await context.Addresses
            .FromSqlInterpolated($"SELECT * FROM addresses WHERE \"Id\" = {initialAddress.Id} FOR UPDATE")
            .SingleAsync(ct);
        var currentOccupancy = await context.Units
            .CountAsync(u => u.AddressId == lockedAddress.Id && (u.Status == "InStock" || u.Status == "AVAILABLE"), ct);

        if (currentOccupancy + request.InitialQuantity > lockedAddress.MaxCapacity)
        {
            throw new BusinessRuleException(
                $"Kabul rafı ({lockedAddress.Code}) parti miktarı için yeterli kapasiteye sahip değil!",
                "INSUFFICIENT_RACK_CAPACITY",
                new { storageType = chemical.StorageType });
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
                AddressId = lockedAddress.Id
            });
        }

        context.Batches.Add(batch);
        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return (await GetByIdAsync(batch.Id, ct))!;
    }
}
