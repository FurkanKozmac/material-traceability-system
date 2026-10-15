using backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Data;

public static class DemoDataSeeder
{
    private static readonly ChemicalSeed[] Chemicals =
    [
        new("DEMO-CHM-01", "Polymer Stabilizer M-01", "Kurgusal polimer katkısı; yalnızca demo verisi.", "GENERAL"),
        new("DEMO-CHM-02", "Surface Prep Solvent S-02", "Kurgusal yüzey hazırlama solventi.", "SOLVENT"),
        new("DEMO-CHM-03", "Low-VOC Rinse Q-03", "Kurgusal düşük uçuculukta durulama karışımı.", "SOLVENT"),
        new("DEMO-CHM-04", "Degreasing Blend K-04", "Kurgusal yağ alma karışımı.", "SOLVENT"),
        new("DEMO-CHM-05", "Primer Resin P-05", "Kurgusal boya astarı reçinesi.", "PAINT"),
        new("DEMO-CHM-06", "Pigment Carrier P-06", "Kurgusal pigment taşıyıcı.", "PAINT"),
        new("DEMO-CHM-07", "Clear Coat Binder P-07", "Kurgusal son kat bağlayıcısı.", "PAINT"),
        new("DEMO-CHM-08", "Reactive Diluent F-08", "Kurgusal yanıcı reaktif seyreltici.", "FLAMMABLE"),
        new("DEMO-CHM-09", "Fast-Cure Additive F-09", "Kurgusal hızlı kür katkısı.", "FLAMMABLE"),
        new("DEMO-CHM-10", "Buffer Solution C-10", "Kurgusal korozif tampon çözeltisi.", "CORROSIVE"),
        new("DEMO-CHM-11", "Etching Compound C-11", "Kurgusal yüzey aşındırma bileşiği.", "CORROSIVE"),
        new("DEMO-CHM-12", "Cleaning Agent G-12", "Kurgusal genel amaçlı temizlik maddesi.", "GENERAL")
    ];

    private static readonly AddressSeed[] Addresses =
    [
        new("DEMO-RACK-GEN-01", "GENERAL"), new("DEMO-RACK-GEN-02", "GENERAL"),
        new("DEMO-RACK-SOL-01", "SOLVENT"), new("DEMO-RACK-SOL-02", "SOLVENT"),
        new("DEMO-RACK-FLM-01", "FLAMMABLE"), new("DEMO-RACK-FLM-02", "FLAMMABLE"),
        new("DEMO-RACK-COR-01", "CORROSIVE"), new("DEMO-RACK-COR-02", "CORROSIVE"),
        new("DEMO-RACK-PNT-01", "PAINT"), new("DEMO-RACK-PNT-02", "PAINT")
    ];

    private static readonly BatchSeed[] Batches =
    [
        new("DEMO-BAT-001", "DEMO-CHM-01", 3, 180),
        new("DEMO-BAT-002", "DEMO-CHM-02", 4, 10),
        new("DEMO-BAT-003", "DEMO-CHM-03", 2, -5),
        new("DEMO-BAT-004", "DEMO-CHM-05", 3, 90),
        new("DEMO-BAT-005", "DEMO-CHM-08", 3, 365),
        new("DEMO-BAT-006", "DEMO-CHM-10", 2, 120),
        new("DEMO-BAT-007", "DEMO-CHM-06", 2, 240),
        new("DEMO-BAT-008", "DEMO-CHM-09", 2, 40)
    ];

    private static readonly UnitSeed[] Units =
    [
        new("BAR-DEMO-UNIT-001", "DEMO-BAT-001", "DEMO-RACK-GEN-01"),
        new("BAR-DEMO-UNIT-002", "DEMO-BAT-001", "DEMO-RACK-GEN-01"),
        new("BAR-DEMO-UNIT-003", "DEMO-BAT-001", "DEMO-RACK-GEN-01"),
        new("BAR-DEMO-UNIT-004", "DEMO-BAT-002", "DEMO-RACK-SOL-01"),
        new("BAR-DEMO-UNIT-005", "DEMO-BAT-002", "DEMO-RACK-SOL-01"),
        new("BAR-DEMO-UNIT-006", "DEMO-BAT-002", "DEMO-RACK-SOL-01"),
        new("BAR-DEMO-UNIT-007", "DEMO-BAT-002", "DEMO-RACK-SOL-01"),
        new("BAR-DEMO-UNIT-008", "DEMO-BAT-003", "DEMO-RACK-SOL-01"),
        new("BAR-DEMO-UNIT-009", "DEMO-BAT-003", "DEMO-RACK-SOL-01"),
        new("BAR-DEMO-UNIT-010", "DEMO-BAT-004", "DEMO-RACK-PNT-01"),
        new("BAR-DEMO-UNIT-011", "DEMO-BAT-004", "DEMO-RACK-PNT-01"),
        new("BAR-DEMO-UNIT-012", "DEMO-BAT-004", "DEMO-RACK-PNT-01"),
        new("BAR-DEMO-UNIT-013", "DEMO-BAT-005", "DEMO-RACK-FLM-01"),
        new("BAR-DEMO-UNIT-014", "DEMO-BAT-005", "DEMO-RACK-FLM-01"),
        new("BAR-DEMO-UNIT-015", "DEMO-BAT-005", "DEMO-RACK-FLM-01"),
        new("BAR-DEMO-UNIT-016", "DEMO-BAT-006", "DEMO-RACK-COR-01"),
        new("BAR-DEMO-UNIT-017", "DEMO-BAT-006", "DEMO-RACK-COR-01"),
        new("BAR-DEMO-UNIT-018", "DEMO-BAT-007", "DEMO-RACK-PNT-01"),
        new("BAR-DEMO-UNIT-019", "DEMO-BAT-007", "DEMO-RACK-PNT-01"),
        new("BAR-DEMO-UNIT-020", "DEMO-BAT-008", "DEMO-RACK-FLM-01"),
        new("BAR-DEMO-UNIT-021", "DEMO-BAT-008", "DEMO-RACK-FLM-01")
    ];

    private static readonly RelocationSeed[] Relocations =
    [
        new("BAR-DEMO-UNIT-004", "DEMO-RACK-SOL-01", "DEMO-RACK-SOL-02"),
        new("BAR-DEMO-UNIT-010", "DEMO-RACK-PNT-01", "DEMO-RACK-PNT-02"),
        new("BAR-DEMO-UNIT-013", "DEMO-RACK-FLM-01", "DEMO-RACK-FLM-02"),
        new("BAR-DEMO-UNIT-016", "DEMO-RACK-COR-01", "DEMO-RACK-COR-02")
    ];

    public static async Task SeedAsync(MtsDbContext context, CancellationToken ct = default)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(ct);
        var now = DateTime.UtcNow;

        var addresses = new Dictionary<string, Address>();
        foreach (var seed in Addresses)
        {
            var address = await context.Addresses.FirstOrDefaultAsync(item => item.Code == seed.Code, ct);
            if (address is null)
            {
                address = new Address { Code = seed.Code, MaxCapacity = 20, StorageType = seed.StorageType };
                context.Addresses.Add(address);
            }
            addresses.Add(seed.Code, address);
        }
        await context.SaveChangesAsync(ct);

        var chemicals = new Dictionary<string, Chemical>();
        foreach (var seed in Chemicals)
        {
            var chemical = await context.Chemicals.FirstOrDefaultAsync(item => item.ChemicalCode == seed.Code, ct);
            if (chemical is null)
            {
                chemical = new Chemical
                {
                    ChemicalCode = seed.Code,
                    Name = seed.Name,
                    Description = seed.Description,
                    StorageType = seed.StorageType,
                    MinStockThreshold = 8,
                    Stopped = false
                };
                context.Chemicals.Add(chemical);
            }
            chemicals.Add(seed.Code, chemical);
        }
        await context.SaveChangesAsync(ct);

        var batches = new Dictionary<string, Batch>();
        foreach (var seed in Batches)
        {
            var batch = await context.Batches.FirstOrDefaultAsync(item => item.BatchNo == seed.Number, ct);
            if (batch is null)
            {
                batch = new Batch
                {
                    BatchNo = seed.Number,
                    Supplier = $"Kurgusal Demo Tedarikçisi {seed.Number[^3..]}",
                    InitialQuantity = seed.Quantity,
                    ExpirationDate = now.AddDays(seed.ExpirationOffsetDays),
                    Chemical = chemicals[seed.ChemicalCode]
                };
                context.Batches.Add(batch);
            }
            batches.Add(seed.Number, batch);
        }
        await context.SaveChangesAsync(ct);

        var occupiedByAddress = await context.Units
            .Where(unit => unit.AddressId.HasValue && (unit.Status == "InStock" || unit.Status == "AVAILABLE"))
            .GroupBy(unit => unit.AddressId!.Value)
            .Select(group => new { AddressId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.AddressId, item => item.Count, ct);
        var units = new Dictionary<string, Unit>();
        foreach (var seed in Units)
        {
            var unit = await context.Units.FirstOrDefaultAsync(item => item.Barcode == seed.Barcode, ct);
            if (unit is null)
            {
                var batch = batches[seed.BatchNumber];
                var address = addresses[seed.AddressCode];
                var chemicalStorageType = await context.Batches
                    .Where(item => item.Id == batch.Id)
                    .Select(item => item.Chemical.StorageType)
                    .SingleAsync(ct);
                var occupancy = occupiedByAddress.GetValueOrDefault(address.Id);
                if (!string.Equals(address.StorageType, chemicalStorageType, StringComparison.OrdinalIgnoreCase) ||
                    occupancy >= address.MaxCapacity)
                    continue;

                unit = new Unit
                {
                    Barcode = seed.Barcode,
                    Status = "InStock",
                    Version = 1,
                    Batch = batch,
                    Address = address
                };
                context.Units.Add(unit);
                occupiedByAddress[address.Id] = occupancy + 1;
            }
            units.Add(seed.Barcode, unit);
        }
        await context.SaveChangesAsync(ct);

        foreach (var seed in Relocations)
        {
            if (!units.TryGetValue(seed.UnitBarcode, out var unit))
                continue;
            var source = addresses[seed.SourceAddressCode];
            var target = addresses[seed.TargetAddressCode];
            if (unit.AddressId != source.Id || unit.Status is not ("InStock" or "AVAILABLE"))
                continue;

            var relocationData = await context.Units
                .Where(item => item.Id == unit.Id)
                .Select(item => new
                {
                    item.Batch.ExpirationDate,
                    item.Batch.Chemical.Stopped,
                    ChemicalStorageType = item.Batch.Chemical.StorageType
                })
                .SingleAsync(ct);
            if (relocationData.Stopped ||
                relocationData.ExpirationDate < now ||
                !string.Equals(target.StorageType, relocationData.ChemicalStorageType, StringComparison.OrdinalIgnoreCase))
                continue;

            var targetOccupancy = await context.Units.CountAsync(item => item.AddressId == target.Id &&
                (item.Status == "InStock" || item.Status == "AVAILABLE"), ct);
            if (targetOccupancy >= target.MaxCapacity)
                continue;

            var pendingTaskExists = await context.RelocationTasks
                .AnyAsync(task => task.UnitId == unit.Id && task.Status == "Pending", ct);
            if (pendingTaskExists)
                continue;

            context.RelocationTasks.Add(new RelocationTask
            {
                Unit = unit,
                FromAddress = source,
                ToAddress = target,
                Status = "Pending",
                CreatedAt = now
            });
        }

        await context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private sealed record ChemicalSeed(string Code, string Name, string Description, string StorageType);
    private sealed record AddressSeed(string Code, string StorageType);
    private sealed record BatchSeed(string Number, string ChemicalCode, int Quantity, int ExpirationOffsetDays);
    private sealed record UnitSeed(string Barcode, string BatchNumber, string AddressCode);
    private sealed record RelocationSeed(string UnitBarcode, string SourceAddressCode, string TargetAddressCode);
}
