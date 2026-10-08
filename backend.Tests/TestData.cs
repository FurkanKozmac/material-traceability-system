using backend.Data;
using backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests;

internal static class TestData
{
    public static async Task<(Chemical Chemical, Batch Batch, Address Address, Unit Unit)> AddUnitAsync(
        MtsDbContext context,
        string storageType = "GENERAL",
        string status = "InStock",
        DateTime? expirationDate = null,
        bool chemicalStopped = false,
        int addressCapacity = 5)
    {
        var chemical = new Chemical
        {
            Name = $"Chemical-{Guid.NewGuid():N}",
            ChemicalCode = $"CHEM-{Guid.NewGuid():N}",
            StorageType = storageType,
            Stopped = chemicalStopped
        };
        var batch = new Batch
        {
            BatchNo = $"LOT-{Guid.NewGuid():N}",
            Chemical = chemical,
            InitialQuantity = 1,
            ExpirationDate = expirationDate
        };
        var address = new Address
        {
            Code = $"ADDRESS-{Guid.NewGuid():N}",
            StorageType = storageType,
            MaxCapacity = addressCapacity
        };
        var unit = new Unit
        {
            Barcode = $"UNIT-{Guid.NewGuid():N}",
            Batch = batch,
            Address = address,
            Status = status,
            Version = 1
        };

        context.Units.Add(unit);
        await context.SaveChangesAsync();
        return (chemical, batch, address, unit);
    }

    public static async Task EnsureCreatedAsync(MtsDbContext context) =>
        await context.Database.EnsureCreatedAsync();
}
