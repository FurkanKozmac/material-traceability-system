using System.IO.Compression;
using backend.Data;
using backend.Entities;
using backend.Services.Implementations.Excel;
using MiniExcelLibs;

namespace backend.Tests;

public class InventoryReportServiceTests
{
    [Fact]
    public async Task Export_ProducesValidWorkbookWithAuditStatusesAndRackRows()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var context = database.Context;
        var today = DateTime.UtcNow.Date;

        var solventRack1 = new Address { Code = "RACK-SOL-01", StorageType = "SOLVENT", MaxCapacity = 100 };
        var solventRack2 = new Address { Code = "RACK-SOL-02", StorageType = "SOLVENT", MaxCapacity = 100 };
        var corrosiveRack = new Address { Code = "RACK-COR-01", StorageType = "CORROSIVE", MaxCapacity = 100 };
        var paintRack = new Address { Code = "RACK-PAINT-01", StorageType = "PAINT", MaxCapacity = 100 };
        context.Addresses.AddRange(solventRack1, solventRack2, corrosiveRack, paintRack);

        AddBatch(context, "CHEM-ACE", "Aseton", "SOLVENT", 0, "LOT-EXP",
            today.AddDays(-2),
            (solventRack1, "InStock"),
            (solventRack1, "InStock"));

        AddBatch(context, "CHEM-NAOH", "Sodyum Hidroksit", "CORROSIVE", 5, "LOT-CRIT",
            today.AddDays(30),
            (corrosiveRack, "InStock"));

        AddBatch(context, "CHEM-PAINT", "Boya", "PAINT", 1, "LOT-NORM",
            today.AddDays(30),
            (paintRack, "InStock"));

        AddBatch(context, "CHEM-THN", "Tiner", "SOLVENT", 0, "LOT-SPLIT",
                today.AddDays(30),
                (solventRack1, "InStock"),
                (solventRack2, "InStock"));

        AddBatch(context, "CHEM-CONSUMED", "Tüketilmiş", "GENERAL", 10, "LOT-CONSUMED",
            today.AddDays(30),
            (solventRack1, "Depleted"));

        await context.SaveChangesAsync();
        var service = new InventoryReportService(context);

        var bytes = await service.ExportAuditReportAsync();

        Assert.NotEmpty(bytes);
        using var stream = new MemoryStream(bytes);
        var rows = stream.Query(useHeaderRow: true, sheetName: "Envanter")
            .Cast<IDictionary<string, object>>()
            .ToList();

        Assert.Equal(5, rows.Count);
        Assert.Equal("Süresi Geçmiş", Convert.ToString(rows[0]["Durum"]));
        Assert.Equal("LOT-EXP", Convert.ToString(rows[0]["Parti No (Batch No)"]));
        Assert.Equal(2, Convert.ToInt32(rows[0]["Mevcut Stok Miktarı"]));
        Assert.Equal("Kritik Eşik Altı", Convert.ToString(rows[1]["Durum"]));
        Assert.Equal("LOT-CRIT", Convert.ToString(rows[1]["Parti No (Batch No)"]));

        var splitRows = rows
            .Where(row => string.Equals(Convert.ToString(row["Parti No (Batch No)"]), "LOT-SPLIT",
                StringComparison.Ordinal))
            .ToList();
        Assert.Equal(2, splitRows.Count);
        Assert.Equal(2, splitRows
            .Select(row => Convert.ToString(row["Bulunduğu Raf / Adres"]))
            .Distinct()
            .Count());
        Assert.All(splitRows, row => Assert.Equal(1, Convert.ToInt32(row["Mevcut Stok Miktarı"])));

        Assert.DoesNotContain(rows, row =>
            string.Equals(Convert.ToString(row["Parti No (Batch No)"]), "LOT-CONSUMED",
                StringComparison.Ordinal));
    }

    [Fact]
    public async Task Export_EmptyInventory_StillProducesHeaderOnlyWorkbookWithColumnWidths()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var service = new InventoryReportService(database.Context);

        var bytes = await service.ExportAuditReportAsync();

        Assert.NotEmpty(bytes);
        using var stream = new MemoryStream(bytes);
        var headerRow = stream.Query(useHeaderRow: false, sheetName: "Envanter")
            .Cast<IDictionary<string, object>>()
            .First();
        var headers = headerRow.Values
            .Select(value => Convert.ToString(value))
            .ToList();

        Assert.Contains("Parti No (Batch No)", headers);
        Assert.Contains("Kimyasal Kodu & Adı", headers);
        Assert.Contains("Depolama Tipi", headers);
        Assert.Contains("Bulunduğu Raf / Adres", headers);
        Assert.Contains("Mevcut Stok Miktarı", headers);
        Assert.Contains("Son Kullanma Tarihi (SKT)", headers);
        Assert.Contains("Durum", headers);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var sheet = archive.GetEntry("xl/worksheets/sheet1.xml");
        Assert.NotNull(sheet);
        using var reader = new StreamReader(sheet.Open());
        var sheetXml = await reader.ReadToEndAsync();
        Assert.Contains("<x:cols>", sheetXml);
    }

    private static Batch AddBatch(
        MtsDbContext context,
        string chemicalCode,
        string chemicalName,
        string storageType,
        int minStockThreshold,
        string batchNo,
        DateTime? expirationDate,
        params (Address Address, string Status)[] units)
    {
        var chemical = new Chemical
        {
            ChemicalCode = chemicalCode,
            Name = chemicalName,
            StorageType = storageType,
            MinStockThreshold = minStockThreshold
        };
        var batch = new Batch
        {
            BatchNo = batchNo,
            Chemical = chemical,
            InitialQuantity = units.Length,
            ExpirationDate = expirationDate
        };

        foreach (var (address, status) in units)
        {
            batch.Units.Add(new Unit
            {
                Barcode = $"UNIT-{Guid.NewGuid():N}",
                Status = status,
                Version = 1,
                Address = address
            });
        }

        context.Batches.Add(batch);
        return batch;
    }
}
