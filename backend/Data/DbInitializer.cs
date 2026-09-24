using backend.Entities;
using Microsoft.EntityFrameworkCore;

namespace backend.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(MtsDbContext context)
    {
        var adminRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Admin");
        if (adminRole is null)
        {
            adminRole = new Role { Name = "Admin" };
            context.Roles.Add(adminRole);
        }

        var operatorRole = await context.Roles.FirstOrDefaultAsync(r => r.Name == "Operator");
        if (operatorRole is null)
        {
            operatorRole = new Role { Name = "Operator" };
            context.Roles.Add(operatorRole);
        }

        await context.SaveChangesAsync();
        
        if (!await context.Users.AnyAsync(u => u.Username == "admin"))
        {
            var adminUser = new User
            {
                Username = "admin",
                Password = "admin123",
                AuthSource = "DB",
                Active = true,
                Roles = [adminRole]
            };
            context.Users.Add(adminUser);
            await context.SaveChangesAsync();
        }

        var existingAdmin = await context.Users
            .Include(u => u.Roles)
            .FirstAsync(u => u.Username == "admin");

        if (!existingAdmin.Roles.Any(r => r.Name == "Admin"))
        {
            existingAdmin.Roles.Add(adminRole);
            await context.SaveChangesAsync();
        }

        if (!await context.Addresses.AnyAsync())
        {
            context.Addresses.AddRange(
                new Address { Code = "RAF-A101", MaxCapacity = 10, StorageType = "GENERAL" },
                new Address { Code = "RAF-A102", MaxCapacity = 10, StorageType = "PAINT" },
                new Address { Code = "RAF-B201", MaxCapacity = 20, StorageType = "FLAMMABLE" },
                new Address { Code = "RAF-C301", MaxCapacity = 20, StorageType = "CORROSIVE" }
            );
            await context.SaveChangesAsync();
        }

        // Demo Dump Data Seeding
        if (!await context.Chemicals.AnyAsync())
        {
            var chemSolvent = new Chemical
            {
                Name = "Tiner / Endüstriyel Solvent X",
                ChemicalCode = "SOL-001",
                Description = "Yüksek saflıkta temizleme ve seyreltme solventi",
                StorageType = "GENERAL",
                Stopped = false
            };
            var chemPaint = new Chemical
            {
                Name = "Epoksi Astar Boya - Kırmızı",
                ChemicalCode = "PNT-002",
                Description = "İki bileşenli korozyon önleyici epoksi astar",
                StorageType = "PAINT",
                Stopped = false
            };
            var chemAcetone = new Chemical
            {
                Name = "Aseton Yüksek Saflık %99.5",
                ChemicalCode = "FLM-003",
                Description = "Hızlı buharlaşan yanıcı çözücü",
                StorageType = "FLAMMABLE",
                Stopped = false
            };
            var chemAcid = new Chemical
            {
                Name = "Hidroklorik Asit Çözeltisi %37",
                ChemicalCode = "COR-004",
                Description = "Endüstriyel yüzey işleme ve asitleme kimyasalı",
                StorageType = "CORROSIVE",
                Stopped = false
            };

            context.Chemicals.AddRange(chemSolvent, chemPaint, chemAcetone, chemAcid);
            await context.SaveChangesAsync();

            var batchSolvent = new Batch
            {
                BatchNo = "BAT-2026-SOL10",
                Supplier = "SolventKimya A.Ş.",
                InitialQuantity = 10,
                ExpirationDate = DateTime.UtcNow.AddYears(2),
                ChemicalId = chemSolvent.Id
            };
            var batchPaint = new Batch
            {
                BatchNo = "BAT-2026-PNT05",
                Supplier = "AkzoKaplama Ltd.",
                InitialQuantity = 5,
                ExpirationDate = DateTime.UtcNow.AddMonths(18),
                ChemicalId = chemPaint.Id
            };
            var batchAcetone = new Batch
            {
                BatchNo = "BAT-2026-FLM08",
                Supplier = "PetroKimya Sanayi",
                InitialQuantity = 8,
                ExpirationDate = DateTime.UtcNow.AddMonths(12),
                ChemicalId = chemAcetone.Id
            };
            var batchExpired = new Batch
            {
                BatchNo = "BAT-2025-EXPIRED",
                Supplier = "EskiTedarik A.Ş.",
                InitialQuantity = 2,
                ExpirationDate = DateTime.UtcNow.AddDays(-30),
                ChemicalId = chemAcid.Id
            };

            context.Batches.AddRange(batchSolvent, batchPaint, batchAcetone, batchExpired);
            await context.SaveChangesAsync();

            var addressGeneral = await context.Addresses.FirstAsync(a => a.Code == "RAF-A101");
            var addressPaint = await context.Addresses.FirstAsync(a => a.Code == "RAF-A102");
            var addressFlammable = await context.Addresses.FirstAsync(a => a.Code == "RAF-B201");
            var addressCorrosive = await context.Addresses.FirstAsync(a => a.Code == "RAF-C301");

            var units = new List<Unit>();

            for (int i = 1; i <= 6; i++)
            {
                units.Add(new Unit
                {
                    Barcode = $"UNT-SOL-{i:D3}",
                    Status = "InStock",
                    Version = 1,
                    BatchId = batchSolvent.Id,
                    AddressId = addressGeneral.Id
                });
            }

            for (int i = 1; i <= 4; i++)
            {
                units.Add(new Unit
                {
                    Barcode = $"UNT-PNT-{i:D3}",
                    Status = "InStock",
                    Version = 1,
                    BatchId = batchPaint.Id,
                    AddressId = addressPaint.Id
                });
            }

            for (int i = 1; i <= 5; i++)
            {
                units.Add(new Unit
                {
                    Barcode = $"UNT-FLM-{i:D3}",
                    Status = "InStock",
                    Version = 1,
                    BatchId = batchAcetone.Id,
                    AddressId = addressFlammable.Id
                });
            }

            units.Add(new Unit
            {
                Barcode = "UNT-EXP-001",
                Status = "InStock",
                Version = 1,
                BatchId = batchExpired.Id,
                AddressId = addressCorrosive.Id
            });

            context.Units.AddRange(units);
            await context.SaveChangesAsync();

            var sampleRelocationUnit = units[0];
            var relocationTask = new RelocationTask
            {
                UnitId = sampleRelocationUnit.Id,
                FromAddressId = addressGeneral.Id,
                ToAddressId = addressGeneral.Id, // same type
                Status = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            context.RelocationTasks.Add(relocationTask);
            await context.SaveChangesAsync();
        }

        await ReconcileIndustrialRackDataAsync(context);
    }

    private static async Task ReconcileIndustrialRackDataAsync(MtsDbContext context)
    {
        var desiredRacks = new Dictionary<string, (int Capacity, string StorageType)>
        {
            ["RACK-P-01"] = (20, "PAINT"),
            ["RACK-P-02"] = (20, "PAINT"),
            ["RACK-S-01"] = (15, "SOLVENT"),
            ["RACK-F-01"] = (10, "FLAMMABLE"),
            ["RACK-C-01"] = (10, "CORROSIVE")
        };

        var addresses = await context.Addresses.Include(a => a.Units).ToListAsync();
        foreach (var (code, definition) in desiredRacks)
        {
            var address = addresses.FirstOrDefault(a => a.Code == code);
            if (address is null)
            {
                address = new Address { Code = code };
                context.Addresses.Add(address);
                addresses.Add(address);
            }

            address.MaxCapacity = definition.Capacity;
            address.StorageType = definition.StorageType;
        }

        await context.SaveChangesAsync();

        var targetByStorageType = desiredRacks
            .GroupBy(pair => pair.Value.StorageType)
            .ToDictionary(group => group.Key, group => addresses.First(a => a.Code == group.First().Key));

        var legacyAddresses = addresses
            .Where(address => !desiredRacks.ContainsKey(address.Code))
            .ToList();

        foreach (var legacyAddress in legacyAddresses)
        {
            var storageType = legacyAddress.StorageType == "GENERAL" ? "SOLVENT" : legacyAddress.StorageType;
            if (!targetByStorageType.TryGetValue(storageType, out var targetAddress))
            {
                targetAddress = targetByStorageType["SOLVENT"];
            }

            var activeUnits = legacyAddress.Units
                .Where(unit => unit.Status != "Depleted")
                .ToList();
            var availableSlots = targetAddress.MaxCapacity - targetAddress.Units.Count(unit => unit.Status != "Depleted");

            foreach (var unit in activeUnits.Take(Math.Max(availableSlots, 0)))
            {
                unit.AddressId = targetAddress.Id;
                targetAddress.Units.Add(unit);
            }

            foreach (var unit in activeUnits.Skip(Math.Max(availableSlots, 0)))
            {
                unit.Status = "Depleted";
                unit.ConsumedAt ??= DateTime.UtcNow;
                unit.AddressId = null;
            }

            var relatedTasks = await context.RelocationTasks
                .Where(task => task.FromAddressId == legacyAddress.Id || task.ToAddressId == legacyAddress.Id)
                .ToListAsync();
            context.RelocationTasks.RemoveRange(relatedTasks);
            context.Addresses.Remove(legacyAddress);
        }

        var clearCoat = await context.Chemicals.FirstOrDefaultAsync(c => c.ChemicalCode == "CC-202");
        if (clearCoat is null)
        {
            context.Chemicals.Add(new Chemical
            {
                ChemicalCode = "CC-202",
                Name = "Clear Coat",
                Description = "Otomotiv gövde boyama için son kat vernik boyası",
                StorageType = "PAINT",
                Stopped = false
            });
        }
        else
        {
            clearCoat.StorageType = "PAINT";
            clearCoat.Stopped = false;
        }

        await EnsureChemicalAsync(
            context,
            "PP-101",
            "Primer Paint",
            "Otomotiv gövde boyama için astar boya",
            "PAINT");
        await EnsureChemicalAsync(
            context,
            "SL-301",
            "Industrial Solvent",
            "Endüstriyel solvent ve tiner karışımı",
            "SOLVENT");

        var generalChemicals = await context.Chemicals
            .Where(chemical => chemical.StorageType == "GENERAL")
            .ToListAsync();
        foreach (var chemical in generalChemicals)
            chemical.StorageType = "SOLVENT";

        await context.SaveChangesAsync();
    }

    private static async Task EnsureChemicalAsync(
        MtsDbContext context,
        string chemicalCode,
        string name,
        string description,
        string storageType)
    {
        var chemical = await context.Chemicals.FirstOrDefaultAsync(c => c.ChemicalCode == chemicalCode);
        if (chemical is null)
        {
            context.Chemicals.Add(new Chemical
            {
                ChemicalCode = chemicalCode,
                Name = name,
                Description = description,
                StorageType = storageType,
                Stopped = false
            });
            return;
        }

        chemical.Name = name;
        chemical.Description = description;
        chemical.StorageType = storageType;
        chemical.Stopped = false;
    }
}
