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
    }
}
