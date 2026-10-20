using backend.Data;
using backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Implementations;

internal static class AddressLockHelper
{
    public static async Task<List<Address>> LockAsync(
        MtsDbContext context,
        IEnumerable<long> addressIds,
        CancellationToken ct = default)
    {
        var ids = addressIds.Distinct().OrderBy(id => id).ToList();
        if (ids.Count == 0)
            return [];

        if (!context.Database.IsNpgsql())
        {
            return await context.Addresses
                .Where(address => ids.Contains(address.Id))
                .ToListAsync(ct);
        }

        var lockedAddresses = new List<Address>(ids.Count);
        foreach (var id in ids)
        {
            var address = await context.Addresses
                .FromSqlInterpolated($"SELECT * FROM addresses WHERE \"Id\" = {id} FOR UPDATE")
                .SingleOrDefaultAsync(ct);
            if (address is not null)
                lockedAddresses.Add(address);
        }

        return lockedAddresses;
    }
}
