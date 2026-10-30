using System.IO.Compression;
using System.Text;
using backend.Common.Exceptions;
using backend.Data;
using backend.DTOs;
using backend.Entities;
using backend.Services.Implementations.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MiniExcelLibs;
using MiniExcelLibs.OpenXml;

namespace backend.Tests;

public class ChemicalExcelServiceTests
{
    private const string CodeHeader = "Kimyasal Kodu *";
    private const string NameHeader = "Kimyasal Adı *";
    private const string StorageHeader = "Depolama Tipi *";
    private const string ThresholdHeader = "Kritik Stok Eşiği";
    private const string DescriptionHeader = "Açıklama / İSG Notu";

    [Fact]
    public async Task Template_ContainsExpectedSheetsHeadersAndExampleRow()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var service = CreateService(database.Context);

        var bytes = await service.CreateTemplateAsync();

        Assert.NotEmpty(bytes);
        using var stream = new MemoryStream(bytes);
        var sheetNames = stream.GetSheetNames();
        Assert.Contains("Kimyasallar", sheetNames);
        Assert.Contains("Kılavuz", sheetNames);

        stream.Position = 0;
        var rows = stream.Query(useHeaderRow: true, sheetName: "Kimyasallar")
            .Cast<IDictionary<string, object>>()
            .ToList();

        var row = Assert.Single(rows);
        Assert.Equal("ORNEK-KIM-001", Convert.ToString(row[CodeHeader]));
        Assert.Equal("Örnek Kimyasal", Convert.ToString(row[NameHeader]));
        Assert.Equal("GENERAL", Convert.ToString(row[StorageHeader]));
        Assert.Equal(10, Convert.ToInt32(row[ThresholdHeader]));
        Assert.Contains("GENERAL, SOLVENT, PAINT, FLAMMABLE, CORROSIVE",
            Convert.ToString(row[DescriptionHeader]));
    }

    [Fact]
    public async Task Import_ValidRows_InsertsChemicalsWithDefaults()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var service = CreateService(database.Context);
        await using var workbook = await CreateWorkbookAsync(
            Row("CHEM-001", "Aseton", "solvent", 5, "Yanıcı"),
            Row("CHEM-002", "Sodyum Hidroksit", "CORROSIVE"));

        var result = await service.ImportAsync(workbook);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.InsertCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.False(result.DryRun);
        Assert.True(result.Committed);
        Assert.Empty(result.Errors);
        Assert.All(result.Rows, row => Assert.Equal(ExcelImportRowStatus.Insert, row.Status));

        var chemicals = await database.Context.Chemicals
            .OrderBy(chemical => chemical.ChemicalCode)
            .ToListAsync();

        Assert.Collection(chemicals,
            first =>
            {
                Assert.Equal("CHEM-001", first.ChemicalCode);
                Assert.Equal("Aseton", first.Name);
                Assert.Equal("SOLVENT", first.StorageType);
                Assert.Equal(5, first.MinStockThreshold);
                Assert.Equal("Yanıcı", first.Description);
                Assert.False(first.Stopped);
            },
            second =>
            {
                Assert.Equal("CHEM-002", second.ChemicalCode);
                Assert.Equal("CORROSIVE", second.StorageType);
                Assert.Equal(10, second.MinStockThreshold);
            });
    }

    [Fact]
    public async Task Import_InvalidStorageAndDuplicateCode_ReportsRowLevelErrorsAndWritesNothing()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var service = CreateService(database.Context);
        await using var workbook = await CreateWorkbookAsync(
            Row("CHEM-DUP", "Aseton", "SOLVENT", 5),
            Row("CHEM-BAD", "Geçersiz", "ACID", -1),
            Row("CHEM-DUP", "Tekrar", "GENERAL", 0));

        var result = await service.ImportAsync(workbook);

        Assert.Equal(3, result.TotalRows);
        Assert.Equal(1, result.InsertCount);
        Assert.Equal(2, result.FailedCount);
        Assert.False(result.Committed);
        Assert.Equal(2, result.Errors.Select(error => error.RowNumber).Distinct().Count());
        Assert.Contains(result.Errors, error =>
            error.RowNumber == 3 &&
            error.Field == StorageHeader &&
            error.Message.Contains("Geçersiz depolama tipi", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error =>
            error.RowNumber == 4 &&
            error.Field == CodeHeader &&
            error.Message.Contains("2. satırda", StringComparison.OrdinalIgnoreCase));

        // All-or-nothing: the valid first row is not written either.
        Assert.Empty(await database.Context.Chemicals.ToListAsync());
    }

    [Fact]
    public async Task Import_CodeAlreadyInDatabase_IsSkippedCaseInsensitivelyWithoutUpdate()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        database.Context.Chemicals.Add(new Chemical
        {
            Name = "Mevcut",
            ChemicalCode = "CHEM-EXIST",
            StorageType = "GENERAL"
        });
        await database.Context.SaveChangesAsync();
        var service = CreateService(database.Context);
        await using var workbook = await CreateWorkbookAsync(
            Row(" chem-exist ", "Yeni Ad", "GENERAL", 1));

        var result = await service.ImportAsync(workbook);

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.InsertCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(0, result.FailedCount);
        Assert.True(result.Committed);
        Assert.Empty(result.Errors);
        var row = Assert.Single(result.Rows);
        Assert.Equal(2, row.RowNumber);
        Assert.Equal(ExcelImportRowStatus.Skip, row.Status);
        var chemical = Assert.Single(await database.Context.Chemicals.AsNoTracking().ToListAsync());
        Assert.Equal("Mevcut", chemical.Name);
    }

    [Fact]
    public async Task Import_BlankRow_PreservesPhysicalExcelRowNumbers()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var service = CreateService(database.Context);
        await using var workbook = await CreateWorkbookAsync(
            Row("CHEM-OK", "Geçerli", "GENERAL", 1),
            EmptyRow(),
            Row("CHEM-BAD", "Geçersiz", "UNKNOWN", 1));

        var result = await service.ImportAsync(workbook);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.InsertCount);
        Assert.Equal(1, result.FailedCount);
        var error = Assert.Single(result.Errors);
        Assert.Equal(4, error.RowNumber);
        Assert.Equal(StorageHeader, error.Field);
    }

    [Fact]
    public async Task Import_MissingRequiredHeaders_ReturnsHeaderError()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var service = CreateService(database.Context);
        await using var workbook = await CreateWorkbookAsync(
            new Dictionary<string, object?>
            {
                [CodeHeader] = "CHEM-001"
            });

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ImportAsync(workbook));

        Assert.Equal("INVALID_EXCEL_HEADERS", exception.ErrorCode);
        Assert.NotNull(exception.Details);
    }

    [Fact]
    public async Task Import_InvalidFile_ReturnsInvalidExcelFileError()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var service = CreateService(database.Context);
        await using var invalidFile = new MemoryStream([1, 2, 3, 4]);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ImportAsync(invalidFile));

        Assert.Equal("INVALID_EXCEL_FILE", exception.ErrorCode);
    }

    [Fact]
    public async Task Import_HeaderOnlyWorkbook_ReturnsEmptyFileError()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var service = CreateService(database.Context);
        await using var workbook = await CreateWorkbookAsync(EmptyRow());

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ImportAsync(workbook));

        Assert.Equal("EMPTY_EXCEL_FILE", exception.ErrorCode);
    }

    [Fact]
    public async Task Import_DryRun_ReportsPreviewAndDoesNotChangeDatabase()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        await SeedChemicalAsync(database.Context, "CHEM-EXIST");
        var service = CreateService(database.Context);
        await using var workbook = await CreateWorkbookAsync(
            Row("CHEM-NEW", "Yeni", "GENERAL"),
            Row("CHEM-EXIST", "Mevcut", "GENERAL"),
            Row("CHEM-BAD", "Hatalı", "ACID"));

        var result = await service.ImportAsync(workbook, dryRun: true);

        Assert.True(result.DryRun);
        Assert.False(result.Committed);
        Assert.Equal(3, result.TotalRows);
        Assert.Equal(1, result.InsertCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(1, result.FailedCount);
        Assert.Equal(
            [ExcelImportRowStatus.Insert, ExcelImportRowStatus.Skip, ExcelImportRowStatus.Error],
            result.Rows.Select(row => row.Status));
        var codes = await database.Context.Chemicals.AsNoTracking().Select(c => c.ChemicalCode).ToListAsync();
        Assert.Equal(["CHEM-EXIST"], codes);
    }

    [Fact]
    public async Task Import_DryRunOfValidFile_DoesNotWrite()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var service = CreateService(database.Context);
        await using var workbook = await CreateWorkbookAsync(Row("CHEM-001", "Aseton", "SOLVENT"));

        var result = await service.ImportAsync(workbook, dryRun: true);

        Assert.Equal(1, result.InsertCount);
        Assert.False(result.Committed);
        Assert.Equal(0, await database.Context.Chemicals.CountAsync());
    }

    [Fact]
    public async Task Import_SameFileTwice_SecondRunSkipsEveryRow()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var service = CreateService(database.Context);
        var bytes = (await CreateWorkbookAsync(
            Row("CHEM-001", "Aseton", "SOLVENT"),
            Row("CHEM-002", "Sodyum Hidroksit", "CORROSIVE"))).ToArray();

        var first = await service.ImportAsync(new MemoryStream(bytes));
        var second = await service.ImportAsync(new MemoryStream(bytes));

        Assert.Equal(2, first.InsertCount);
        Assert.Equal(0, second.InsertCount);
        Assert.Equal(2, second.SkippedCount);
        Assert.Equal(0, second.FailedCount);
        Assert.All(second.Rows, row => Assert.Equal(ExcelImportRowStatus.Skip, row.Status));
        Assert.Equal(2, await database.Context.Chemicals.CountAsync());
    }

    [Fact]
    public async Task Template_HeadersMatchImporterSchemaExactly()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var bytes = await CreateService(database.Context).CreateTemplateAsync();

        using var stream = new MemoryStream(bytes);
        var headerRow = stream.Query(useHeaderRow: false, sheetName: "Kimyasallar")
            .Cast<IDictionary<string, object>>()
            .First();

        Assert.Equal(ChemicalExcelColumns.All, headerRow.Values.Select(value => Convert.ToString(value)));
        Assert.Equal([CodeHeader, NameHeader, StorageHeader, ThresholdHeader, DescriptionHeader], ChemicalExcelColumns.All);
    }

    [Fact]
    public async Task Import_TemplateRoundTrip_FilledTemplateImports()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var service = CreateService(database.Context);
        var template = await service.CreateTemplateAsync();

        // Unchanged template: the example row is importable as-is, proving the schema matches.
        var preview = await service.ImportAsync(new MemoryStream(template), dryRun: true);
        Assert.Equal(1, preview.InsertCount);
        Assert.Equal(0, preview.FailedCount);
        Assert.Equal("ORNEK-KIM-001", Assert.Single(preview.Rows).ChemicalCode);

        // Fill the template: keep its header row and guide sheet, replace the example row with real data.
        var filled = await FillTemplateAsync(template,
            new ChemicalTemplateRow { ChemicalCode = "RT-001", Name = "Aseton", StorageType = "SOLVENT", MinStockThreshold = 3, Description = "Yanıcı" },
            new ChemicalTemplateRow { ChemicalCode = "RT-002", Name = "Boya", StorageType = "PAINT" });

        var result = await service.ImportAsync(new MemoryStream(filled));

        Assert.True(result.Committed);
        Assert.Equal(2, result.InsertCount);
        var chemicals = await database.Context.Chemicals.OrderBy(c => c.ChemicalCode).ToListAsync();
        Assert.Collection(chemicals,
            first =>
            {
                Assert.Equal("RT-001", first.ChemicalCode);
                Assert.Equal("SOLVENT", first.StorageType);
                Assert.Equal(3, first.MinStockThreshold);
                Assert.Equal("Yanıcı", first.Description);
            },
            second =>
            {
                Assert.Equal("RT-002", second.ChemicalCode);
                Assert.Equal(10, second.MinStockThreshold);
            });
    }

    [Fact]
    public async Task Import_XlsmWithVbaProject_IsRejected()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var bytes = await ModifyArchiveAsync(await CreateValidWorkbookBytesAsync(), archive =>
        {
            var entry = archive.CreateEntry("xl/vbaProject.bin");
            using var stream = entry.Open();
            stream.Write([0xD0, 0xCF, 0x11, 0xE0]);
        });

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateService(database.Context).ImportAsync(new MemoryStream(bytes)));

        Assert.Equal("MACRO_FILE_NOT_ALLOWED", exception.ErrorCode);
        Assert.Equal(0, await database.Context.Chemicals.CountAsync());
    }

    [Fact]
    public async Task Import_MacroEnabledContentType_IsRejected()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var bytes = await ModifyArchiveAsync(await CreateValidWorkbookBytesAsync(), archive =>
        {
            var entry = archive.GetEntry("[Content_Types].xml")!;
            string xml;
            using (var reader = new StreamReader(entry.Open()))
                xml = reader.ReadToEnd();
            entry.Delete();
            xml = xml.Replace(
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml",
                "application/vnd.ms-excel.sheet.macroEnabled.main+xml");
            using var writer = new StreamWriter(archive.CreateEntry("[Content_Types].xml").Open());
            writer.Write(xml);
        });

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateService(database.Context).ImportAsync(new MemoryStream(bytes)));

        Assert.Equal("MACRO_FILE_NOT_ALLOWED", exception.ErrorCode);
    }

    [Theory]
    [InlineData(new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0, 0, 0, 0 })] // legacy .xls (OLE2)
    [InlineData(new byte[] { 0x4B, 0x6F, 0x64, 0x2C, 0x41, 0x64, 0x0A, 0x41, 0x2C, 0x42, 0x0A })] // CSV text "Kod,Ad\nA,B\n"
    public async Task Import_NonZipContentWithXlsxName_IsRejectedBySignature(byte[] content)
    {
        await using var database = await SqliteTestDatabase.CreateAsync();

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateService(database.Context).ImportAsync(new MemoryStream(content)));

        Assert.Equal("INVALID_EXCEL_FILE", exception.ErrorCode);
    }

    [Fact]
    public async Task Import_ZipThatIsNotASpreadsheet_IsRejected()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(archive.CreateEntry("readme.txt").Open());
            writer.Write("not a workbook");
        }

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateService(database.Context).ImportAsync(new MemoryStream(stream.ToArray())));

        Assert.Equal("INVALID_EXCEL_FILE", exception.ErrorCode);
    }

    [Fact]
    public async Task Import_FileAboveConfiguredSizeLimit_IsRejected()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var bytes = await CreateValidWorkbookBytesAsync();
        var service = CreateService(database.Context, new ExcelImportOptions { MaxFileBytes = bytes.Length - 1 });

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ImportAsync(new MemoryStream(bytes)));

        Assert.Equal("EXCEL_FILE_TOO_LARGE", exception.ErrorCode);
    }

    [Fact]
    public async Task Import_RowsAboveConfiguredLimit_AreRejectedAndNothingWritten()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var service = CreateService(database.Context, new ExcelImportOptions { MaxRows = 2 });
        await using var workbook = await CreateWorkbookAsync(
            Row("CHEM-001", "A", "GENERAL"),
            Row("CHEM-002", "B", "GENERAL"),
            Row("CHEM-003", "C", "GENERAL"));

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ImportAsync(workbook));

        Assert.Equal("IMPORT_ROW_LIMIT_EXCEEDED", exception.ErrorCode);
        Assert.Equal(0, await database.Context.Chemicals.CountAsync());
    }

    [Fact]
    public async Task Import_ArchiveWithTooManyEntries_IsRejected()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var bytes = await CreateValidWorkbookBytesAsync();
        int entryCount;
        using (var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
            entryCount = archive.Entries.Count;
        var service = CreateService(database.Context, new ExcelImportOptions { MaxArchiveEntries = entryCount - 1 });

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ImportAsync(new MemoryStream(bytes)));

        Assert.Equal("INVALID_EXCEL_FILE", exception.ErrorCode);
        Assert.Equal(0, await database.Context.Chemicals.CountAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Import_ArchiveLimits_ApplyToDryRunAndRealImport(bool dryRun)
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var bytes = await CreateValidWorkbookBytesAsync();
        var service = CreateService(database.Context, new ExcelImportOptions { MaxUncompressedBytes = 1024 });

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ImportAsync(new MemoryStream(bytes), dryRun));

        Assert.Equal("INVALID_EXCEL_FILE", exception.ErrorCode);
        Assert.Equal(0, await database.Context.Chemicals.CountAsync());
    }

    [Fact]
    public async Task Import_FilledTemplate_PassesDefaultArchiveLimits()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var defaults = new ExcelImportOptions();
        Assert.Equal(100, defaults.MaxArchiveEntries);
        Assert.Equal(50 * 1024 * 1024, defaults.MaxUncompressedBytes);
        var service = CreateService(database.Context, defaults);
        var template = await service.CreateTemplateAsync();

        var result = await service.ImportAsync(new MemoryStream(template), dryRun: true);

        Assert.Equal(0, result.FailedCount);
    }

    [Fact]
    public async Task Import_ArchiveExpandingBeyondLimit_IsRejected()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var bytes = await ModifyArchiveAsync(await CreateValidWorkbookBytesAsync(), archive =>
        {
            using var stream = archive.CreateEntry("xl/media/pad.bin", CompressionLevel.SmallestSize).Open();
            stream.Write(new byte[4 * 1024 * 1024]);
        });
        var service = CreateService(database.Context, new ExcelImportOptions { MaxUncompressedBytes = 1024 * 1024 });

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.ImportAsync(new MemoryStream(bytes)));

        Assert.Equal("INVALID_EXCEL_FILE", exception.ErrorCode);
    }

    [Fact]
    public async Task Import_ArchiveWithForgedSmallDeclaredSize_CannotInflatePastDeclaredSize()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var bytes = await ModifyArchiveAsync(await CreateValidWorkbookBytesAsync(), archive =>
        {
            using var stream = archive.CreateEntry("xl/media/pad.bin", CompressionLevel.SmallestSize).Open();
            stream.Write(new byte[4 * 1024 * 1024]);
        });
        ForgeDeclaredUncompressedSize(bytes, "xl/media/pad.bin", 16);

        // ZipArchive never inflates an entry past its declared length, so a lying header cannot expand
        // beyond what the declared-size check already accepted.
        using (var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read))
        {
            var inflated = 0L;
            try
            {
                using var stream = archive.GetEntry("xl/media/pad.bin")!.Open();
                var chunk = new byte[81920];
                int read;
                while ((read = stream.Read(chunk)) > 0)
                    inflated += read;
            }
            catch (InvalidDataException)
            {
                // Also acceptable: the runtime rejects the mismatching entry outright.
            }

            Assert.True(inflated <= 16, $"Inflated {inflated} bytes past the declared size.");
        }

        var service = CreateService(database.Context, new ExcelImportOptions { MaxUncompressedBytes = 1024 * 1024 });
        var result = await service.ImportAsync(new MemoryStream(bytes));
        Assert.Equal(1, result.InsertCount);
    }

    [Fact]
    public async Task Import_ThresholdOutOfRangeAndLongDescription_AreReportedPerRow()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var service = CreateService(database.Context);
        await using var workbook = await CreateWorkbookAsync(
            Row("CHEM-001", "A", "GENERAL", 1_000_001),
            Row("CHEM-002", "B", "GENERAL", 1, new string('x', 2001)),
            Row(new string('K', 51), "C", "GENERAL"));

        var result = await service.ImportAsync(workbook);

        Assert.Equal(3, result.FailedCount);
        Assert.Contains(result.Errors, e => e.RowNumber == 2 && e.Field == ThresholdHeader);
        Assert.Contains(result.Errors, e => e.RowNumber == 3 && e.Field == DescriptionHeader);
        Assert.Contains(result.Errors, e => e.RowNumber == 4 && e.Field == CodeHeader);
        Assert.Equal(0, await database.Context.Chemicals.CountAsync());
    }

    [Fact]
    public async Task Import_FormulaCell_IsReadAsPlainCachedValue()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var service = CreateService(database.Context);
        await using var workbook = await CreateWorkbookAsync(Row("=1+1", "Formül", "GENERAL"));

        var result = await service.ImportAsync(workbook);

        Assert.True(result.Committed);
        var chemical = Assert.Single(await database.Context.Chemicals.ToListAsync());
        Assert.Equal("=1+1", chemical.ChemicalCode);
    }

    private static ChemicalExcelService CreateService(MtsDbContext context, ExcelImportOptions? options = null) =>
        new(context, Options.Create(options ?? new ExcelImportOptions()));

    private static async Task SeedChemicalAsync(MtsDbContext context, string code)
    {
        context.Chemicals.Add(new Chemical { Name = "Mevcut", ChemicalCode = code, StorageType = "GENERAL" });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static async Task<byte[]> CreateValidWorkbookBytesAsync()
    {
        await using var workbook = await CreateWorkbookAsync(Row("CHEM-001", "Aseton", "SOLVENT"));
        return workbook.ToArray();
    }

    private static async Task<byte[]> FillTemplateAsync(byte[] template, params ChemicalTemplateRow[] rows)
    {
        using var source = new MemoryStream(template);
        var guide = source.Query<ChemicalTemplateGuideRow>(sheetName: "Kılavuz").ToList();
        var sheets = new Dictionary<string, object>
        {
            ["Kimyasallar"] = rows.ToList(),
            ["Kılavuz"] = guide
        };
        await using var output = new MemoryStream();
        await output.SaveAsAsync(sheets);
        return output.ToArray();
    }

    private static async Task<byte[]> ModifyArchiveAsync(byte[] bytes, Action<ZipArchive> modify)
    {
        var stream = new MemoryStream();
        await stream.WriteAsync(bytes);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
            modify(archive);
        return stream.ToArray();
    }

    // Rewrites the uncompressed size in both the local header and the central directory to simulate a zip bomb
    // whose headers lie about how much data it expands to.
    private static void ForgeDeclaredUncompressedSize(byte[] bytes, string entryName, uint forgedSize)
    {
        var name = Encoding.UTF8.GetBytes(entryName);
        var patched = 0;
        for (var i = 0; i + 46 < bytes.Length; i++)
        {
            if (bytes[i] != 0x50 || bytes[i + 1] != 0x4B)
                continue;

            if (bytes[i + 2] == 0x03 && bytes[i + 3] == 0x04 && NameMatches(bytes, i + 30, BitConverter.ToUInt16(bytes, i + 26), name))
            {
                BitConverter.GetBytes(forgedSize).CopyTo(bytes, i + 22);
                patched++;
            }
            else if (bytes[i + 2] == 0x01 && bytes[i + 3] == 0x02 && NameMatches(bytes, i + 46, BitConverter.ToUInt16(bytes, i + 28), name))
            {
                BitConverter.GetBytes(forgedSize).CopyTo(bytes, i + 24);
                patched++;
            }
        }

        Assert.Equal(2, patched);
    }

    private static bool NameMatches(byte[] bytes, int offset, int length, byte[] name) =>
        length == name.Length && offset + length <= bytes.Length && bytes.AsSpan(offset, length).SequenceEqual(name);

    private static async Task<MemoryStream> CreateWorkbookAsync(params Dictionary<string, object?>[] rows)
    {
        var stream = new MemoryStream();
        await stream.SaveAsAsync(
            rows,
            printHeader: true,
            sheetName: "Kimyasallar",
            configuration: new OpenXmlConfiguration());
        stream.Position = 0;
        return stream;
    }

    private static Dictionary<string, object?> Row(
        string code,
        string name,
        string storageType,
        object? threshold = null,
        string? description = null) =>
        new()
        {
            [CodeHeader] = code,
            [NameHeader] = name,
            [StorageHeader] = storageType,
            [ThresholdHeader] = threshold,
            [DescriptionHeader] = description
        };

    private static Dictionary<string, object?> EmptyRow() =>
        new()
        {
            [CodeHeader] = null,
            [NameHeader] = null,
            [StorageHeader] = null,
            [ThresholdHeader] = null,
            [DescriptionHeader] = null
        };
}
