using backend.Common.Exceptions;
using backend.DTOs;
using backend.Entities;
using backend.Services.Implementations.Excel;
using Microsoft.EntityFrameworkCore;
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
        var service = new ChemicalExcelService(database.Context);

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
        var service = new ChemicalExcelService(database.Context);
        await using var workbook = await CreateWorkbookAsync(
            Row("CHEM-001", "Aseton", "solvent", 5, "Yanıcı"),
            Row("CHEM-002", "Sodyum Hidroksit", "CORROSIVE"));

        var result = await service.ImportAsync(workbook);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(0, result.FailedCount);
        Assert.Empty(result.Errors);

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
    public async Task Import_InvalidStorageAndDuplicateCode_ReportsRowLevelErrorsAndKeepsValidRows()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var service = new ChemicalExcelService(database.Context);
        await using var workbook = await CreateWorkbookAsync(
            Row("CHEM-DUP", "Aseton", "SOLVENT", 5),
            Row("CHEM-BAD", "Geçersiz", "ACID", -1),
            Row("CHEM-DUP", "Tekrar", "GENERAL", 0));

        var result = await service.ImportAsync(workbook);

        Assert.Equal(3, result.TotalRows);
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(2, result.FailedCount);
        Assert.Equal(2, result.Errors.Select(error => error.RowNumber).Distinct().Count());
        Assert.Contains(result.Errors, error =>
            error.RowNumber == 3 &&
            error.Field == StorageHeader &&
            error.Message.Contains("Geçersiz depolama tipi", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Errors, error =>
            error.RowNumber == 4 &&
            error.Field == CodeHeader &&
            error.Message.Contains("2. satırda", StringComparison.OrdinalIgnoreCase));

        var chemicals = await database.Context.Chemicals.ToListAsync();
        var chemical = Assert.Single(chemicals);
        Assert.Equal("CHEM-DUP", chemical.ChemicalCode);
        Assert.Equal("Aseton", chemical.Name);
    }

    [Fact]
    public async Task Import_CodeAlreadyInDatabase_IsRejectedCaseInsensitively()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        database.Context.Chemicals.Add(new Chemical
        {
            Name = "Mevcut",
            ChemicalCode = "CHEM-EXIST",
            StorageType = "GENERAL"
        });
        await database.Context.SaveChangesAsync();
        var service = new ChemicalExcelService(database.Context);
        await using var workbook = await CreateWorkbookAsync(
            Row(" chem-exist ", "Yeni Ad", "GENERAL", 1));

        var result = await service.ImportAsync(workbook);

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(1, result.FailedCount);
        var error = Assert.Single(result.Errors);
        Assert.Equal(2, error.RowNumber);
        Assert.Equal(CodeHeader, error.Field);
        Assert.Contains("sistemde zaten", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, await database.Context.Chemicals.CountAsync());
    }

    [Fact]
    public async Task Import_BlankRow_PreservesPhysicalExcelRowNumbers()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var service = new ChemicalExcelService(database.Context);
        await using var workbook = await CreateWorkbookAsync(
            Row("CHEM-OK", "Geçerli", "GENERAL", 1),
            EmptyRow(),
            Row("CHEM-BAD", "Geçersiz", "UNKNOWN", 1));

        var result = await service.ImportAsync(workbook);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(1, result.FailedCount);
        var error = Assert.Single(result.Errors);
        Assert.Equal(4, error.RowNumber);
        Assert.Equal(StorageHeader, error.Field);
    }

    [Fact]
    public async Task Import_MissingRequiredHeaders_ReturnsHeaderError()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var service = new ChemicalExcelService(database.Context);
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
        var service = new ChemicalExcelService(database.Context);
        await using var invalidFile = new MemoryStream([1, 2, 3, 4]);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ImportAsync(invalidFile));

        Assert.Equal("INVALID_EXCEL_FILE", exception.ErrorCode);
    }

    [Fact]
    public async Task Import_HeaderOnlyWorkbook_ReturnsEmptyFileError()
    {
        await using var database = await SqliteTestDatabase.CreateAsync();
        var service = new ChemicalExcelService(database.Context);
        await using var workbook = await CreateWorkbookAsync(EmptyRow());

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => service.ImportAsync(workbook));

        Assert.Equal("EMPTY_EXCEL_FILE", exception.ErrorCode);
    }

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
