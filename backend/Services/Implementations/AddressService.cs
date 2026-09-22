using backend.Common.Enums;
using backend.Common.Exceptions;
using backend.Data;
using backend.DTOs;
using backend.Entities;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Implementations;

public class AddressService(MtsDbContext context) : IAddressService
{
    public async Task<PagedResult<AddressResponse>> GetAllAsync(int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = context.Addresses.AsNoTracking().OrderBy(a => a.Code);
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .Select(a => new AddressResponse(a.Id, a.Code, a.MaxCapacity, a.Units.Count, a.StorageType))
            .ToListAsync(ct);
        return new PagedResult<AddressResponse>(items, totalCount, page, pageSize);
    }

    public async Task<AddressResponse?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return await context.Addresses
            .AsNoTracking()
            .Where(a => a.Id == id)
            .Select(a => new AddressResponse(a.Id, a.Code, a.MaxCapacity, a.Units.Count, a.StorageType))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<AddressResponse> CreateAsync(CreateAddressRequest request, CancellationToken ct = default)
    {
        if (!Enum.TryParse<StorageType>(request.StorageType, true, out var storageType))
            throw new InvalidOperationException("Geçersiz depolama türü!");

        if (await context.Addresses.AnyAsync(a => a.Code == request.Code, ct))
            throw new InvalidOperationException($"'{request.Code}' kodlu adres zaten tanımlı!");

        var address = new Address
        {
            Code = request.Code,
            MaxCapacity = request.MaxCapacity,
            StorageType = storageType.ToString()
        };
        context.Addresses.Add(address);
        await context.SaveChangesAsync(ct);
        return new AddressResponse(address.Id, address.Code, address.MaxCapacity, 0, address.StorageType);
    }

    public async Task<bool> UpdateStorageTypeAsync(long id, string storageType, CancellationToken ct = default)
    {
        if (!Enum.TryParse<StorageType>(storageType, true, out var parsedStorageType))
            throw new InvalidOperationException("Geçersiz depolama türü!");

        var address = await context.Addresses.FindAsync([id], ct);
        if (address is null) return false;

        var hasIncompatibleUnits = await context.Units
            .AnyAsync(u => u.AddressId == id && u.Batch.Chemical.StorageType != parsedStorageType.ToString(), ct);
        if (hasIncompatibleUnits)
            throw new BusinessRuleException("Raf, mevcut stokların depolama türüyle uyumsuz hale getirilemez.");

        address.StorageType = parsedStorageType.ToString();
        await context.SaveChangesAsync(ct);
        return true;
    }
}
