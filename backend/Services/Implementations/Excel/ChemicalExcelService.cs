using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO.Compression;
using System.Xml;
using backend.Common.Enums;
using backend.Common.Exceptions;
using backend.Data;
using backend.DTOs;
using backend.Entities;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using MiniExcelLibs;
using MiniExcelLibs.OpenXml;

namespace backend.Services.Implementations.Excel;

public sealed class ChemicalExcelService(MtsDbContext context) : IChemicalExcelService
{
    private const int MaxFileBytes = 10 * 1024 * 1024;
    private const int MaxUncompressedBytes = 100 * 1024 * 1024;
    private const int MaxImportRows = 10_000;
    private const int MaxChemicalCodeLength = 50;
    private const int MaxChemicalNameLength = 200;
    private const int DefaultMinStockThreshold = 10;

    private const string DataSheetName = "Kimyasallar";
    private const string GuideSheetName = "Kılavuz";
    private const string CodeField = "Kimyasal Kodu *";
    private const string NameField = "Kimyasal Adı *";
    private const string StorageField = "Depolama Tipi *";
    private const string ThresholdField = "Kritik Stok Eşiği";
    private const string DescriptionField = "Açıklama / İSG Notu";
    private const string StorageTypeList = "GENERAL, SOLVENT, PAINT, FLAMMABLE, CORROSIVE";

    private static readonly Dictionary<string, string[]> HeaderAliases = new()
    {
        [CodeField] = [CodeField, "Kimyasal Kodu"],
        [NameField] = [NameField, "Kimyasal Adı", "Kimyasal Adi"],
        [StorageField] = [StorageField, "Depolama Tipi"],
        [ThresholdField] = [ThresholdField, "Kritik Stok Esigi"],
        [DescriptionField] = [DescriptionField, "Aciklama / ISG Notu", "Açıklama", "Aciklama"]
    };

    private static readonly Dictionary<string, string> HeaderLookup = BuildHeaderLookup();
    private static readonly string[] RequiredFields = [CodeField, NameField, StorageField];
    private static readonly string[] FieldOrder = [CodeField, NameField, StorageField, ThresholdField, DescriptionField];

    public async Task<byte[]> CreateTemplateAsync(CancellationToken ct = default)
    {
        var rows = new List<ChemicalTemplateRow>
        {
            new()
            {
                ChemicalCode = "ORNEK-KIM-001",
                Name = "Örnek Kimyasal",
                StorageType = "GENERAL",
                MinStockThreshold = DefaultMinStockThreshold,
                Description = $"Örnek satırdır; içe aktarmadan önce silin veya kendi verinizle değiştirin. Geçerli depolama tipleri: {StorageTypeList}."
            }
        };

        var guide = new List<ChemicalTemplateGuideRow>
        {
            new() { Topic = "Zorunlu alanlar", Description = "Kimyasal Kodu *, Kimyasal Adı *, Depolama Tipi *" },
            new() { Topic = "Geçerli depolama tipleri", Description = StorageTypeList },
            new() { Topic = "Kritik stok eşiği", Description = "Boş bırakılırsa 10 kabul edilir. 0 veya daha büyük bir tam sayı olmalıdır." },
            new() { Topic = "Örnek satır", Description = "Kimyasallar sayfasındaki örnek satırı içe aktarmadan önce silin veya kendi verinizle değiştirin." },
            new() { Topic = "Dosya biçimi", Description = "Yalnızca .xlsx kabul edilir. En fazla 10 MB ve 10.000 veri satırı işlenir." }
        };

        var sheets = new Dictionary<string, object>
        {
            [DataSheetName] = rows,
            [GuideSheetName] = guide
        };

        await using var stream = new MemoryStream();
        await stream.SaveAsAsync(
            sheets,
            configuration: CreateTemplateConfiguration(),
            cancellationToken: ct);

        return stream.ToArray();
    }

    public async Task<BulkImportResultDto> ImportAsync(Stream stream, CancellationToken ct = default)
    {
        await using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);

        if (buffer.Length == 0)
            throw new BusinessRuleException("Excel dosyası gereklidir.", "EXCEL_FILE_REQUIRED");
        if (buffer.Length > MaxFileBytes)
            throw new BusinessRuleException("Excel dosyası en fazla 10 MB olabilir.", "EXCEL_FILE_TOO_LARGE");

        try
        {
            ValidateArchive(buffer);
            var sheetName = ResolveDataSheet(buffer);
            return await ReadAndImportRowsAsync(buffer, sheetName, ct);
        }
        catch (BusinessRuleException)
        {
            throw;
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or IOException or ArgumentException or XmlException)
        {
            throw new BusinessRuleException(
                "Excel dosyası okunamadı. Lütfen geçerli bir .xlsx dosyası yükleyin.",
                "INVALID_EXCEL_FILE");
        }
    }

    private async Task<BulkImportResultDto> ReadAndImportRowsAsync(
        MemoryStream buffer,
        string sheetName,
        CancellationToken ct)
    {
        var existingCodes = (await context.Chemicals
                .AsNoTracking()
                .Select(chemical => chemical.ChemicalCode)
                .ToListAsync(ct))
            .Select(code => code.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var seenCodes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var errors = new List<ExcelRowErrorDto>();
        var failedRows = new HashSet<int>();
        var validChemicals = new List<Chemical>();
        Dictionary<string, string>? columns = null;
        var totalRows = 0;
        var rowNumber = 1;

        buffer.Position = 0;
        var rows = await buffer.QueryAsync(
            useHeaderRow: false,
            sheetName: sheetName,
            configuration: new OpenXmlConfiguration { IgnoreEmptyRows = false },
            cancellationToken: ct);

        foreach (var rawRow in rows)
        {
            ct.ThrowIfCancellationRequested();
            var row = (IDictionary<string, object?>)rawRow;
            if (columns is null)
            {
                columns = BuildColumnMap(row);
                continue;
            }

            rowNumber++;

            if (IsEmptyRow(row))
                continue;

            totalRows++;
            if (totalRows > MaxImportRows)
            {
                throw new BusinessRuleException(
                    $"Bir seferde en fazla {MaxImportRows:N0} satır içe aktarılabilir.",
                    "IMPORT_ROW_LIMIT_EXCEEDED");
            }

            var code = ToText(GetCell(row, columns, CodeField));
            var name = ToText(GetCell(row, columns, NameField));
            var storageValue = ToText(GetCell(row, columns, StorageField));
            var description = ToText(GetCell(row, columns, DescriptionField));
            var rowHasError = false;

            void AddError(string field, string message, string? value = null)
            {
                rowHasError = true;
                failedRows.Add(rowNumber);
                errors.Add(new ExcelRowErrorDto(rowNumber, field, message, value));
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                AddError(CodeField, "Kimyasal kodu zorunludur.");
            }
            else
            {
                if (code.Length > MaxChemicalCodeLength)
                {
                    AddError(CodeField,
                        $"Kimyasal kodu en fazla {MaxChemicalCodeLength} karakter olabilir.",
                        code);
                }

                if (seenCodes.TryGetValue(code, out var firstRow))
                {
                    AddError(CodeField,
                        $"Bu kimyasal kodu dosyada {firstRow}. satırda zaten kullanılıyor.",
                        code);
                }
                else
                {
                    seenCodes[code] = rowNumber;
                }

                if (existingCodes.Contains(code))
                {
                    AddError(CodeField, "Bu kimyasal kodu sistemde zaten kayıtlı.", code);
                }
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                AddError(NameField, "Kimyasal adı zorunludur.");
            }
            else if (name.Length > MaxChemicalNameLength)
            {
                AddError(NameField,
                    $"Kimyasal adı en fazla {MaxChemicalNameLength} karakter olabilir.",
                    name);
            }

            var storageType = string.Empty;
            if (string.IsNullOrWhiteSpace(storageValue))
            {
                AddError(StorageField,
                    $"Depolama tipi zorunludur. Geçerli tipler: {StorageTypeList}.");
            }
            else
            {
                var matchedStorageType = Enum.GetNames<StorageType>()
                    .FirstOrDefault(candidate => string.Equals(candidate, storageValue, StringComparison.OrdinalIgnoreCase));

                if (matchedStorageType is null)
                {
                    AddError(StorageField,
                        $"Geçersiz depolama tipi: '{storageValue}'. Geçerli tipler: {StorageTypeList}.",
                        storageValue);
                }
                else
                {
                    storageType = matchedStorageType;
                }
            }

            if (!TryReadThreshold(GetCell(row, columns, ThresholdField), out var threshold, out var thresholdError))
            {
                AddError(ThresholdField, thresholdError!, ToText(GetCell(row, columns, ThresholdField)));
            }

            if (rowHasError)
                continue;

            validChemicals.Add(new Chemical
            {
                Name = name!,
                ChemicalCode = code!,
                Description = description,
                StorageType = storageType,
                MinStockThreshold = threshold,
                Stopped = false
            });
        }

        if (columns is null || totalRows == 0)
        {
            throw new BusinessRuleException(
                "Excel dosyasında içe aktarılacak veri satırı bulunamadı.",
                "EMPTY_EXCEL_FILE");
        }

        if (validChemicals.Count > 0)
        {
            await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            try
            {
                context.Chemicals.AddRange(validChemicals);
                await context.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync(ct);
                throw new BusinessRuleException(
                    "İçe aktarma sırasında bir kimyasal kodu başka bir işlem tarafından eklendi. Lütfen tekrar deneyin.",
                    "IMPORT_CONFLICT");
            }
        }

        var orderedErrors = errors
            .OrderBy(error => error.RowNumber)
            .ThenBy(error => Array.IndexOf(FieldOrder, error.Field))
            .ToList();

        return new BulkImportResultDto(
            totalRows,
            validChemicals.Count,
            failedRows.Count,
            orderedErrors);
    }

    private static OpenXmlConfiguration CreateTemplateConfiguration() => new()
    {
        FastMode = true,
        EnableAutoWidth = true,
        MinWidth = 10,
        MaxWidth = 60,
        FreezeRowCount = 1,
        AutoFilter = false,
        TableStyles = TableStyles.Default,
        StyleOptions = new OpenXmlStyleOptions
        {
            HeaderStyle = new OpenXmlHeaderStyle
            {
                BackgroundColor = Color.FromArgb(255, 31, 78, 121),
                WrapText = true,
                HorizontalAlignment = HorizontalCellAlignment.Center,
                VerticalAlignment = VerticalCellAlignment.Center
            },
            HorizontalAlignment = HorizontalCellAlignment.Left,
            VerticalAlignment = VerticalCellAlignment.Center,
            WrapCellContents = true
        }
    };

    private static string ResolveDataSheet(MemoryStream buffer)
    {
        buffer.Position = 0;
        var sheetNames = buffer.GetSheetNames();
        buffer.Position = 0;

        var dataSheet = sheetNames.FirstOrDefault(name =>
            string.Equals(name, DataSheetName, StringComparison.OrdinalIgnoreCase));
        var firstSheet = sheetNames.FirstOrDefault();

        if (dataSheet is null && firstSheet is null)
        {
            throw new BusinessRuleException(
                "Excel dosyasında okunabilir bir sayfa bulunamadı.",
                "INVALID_EXCEL_FILE");
        }

        return dataSheet ?? firstSheet!;
    }

    private static void ValidateArchive(MemoryStream buffer)
    {
        buffer.Position = 0;
        try
        {
            using var archive = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: true);
            long totalUncompressedBytes = 0;

            foreach (var entry in archive.Entries)
            {
                if (entry.Length > MaxUncompressedBytes - totalUncompressedBytes)
                {
                    throw new BusinessRuleException(
                        "Excel dosyasının açılmış boyutu güvenli sınırı aşıyor.",
                        "INVALID_EXCEL_FILE");
                }

                totalUncompressedBytes += entry.Length;
            }
        }
        finally
        {
            buffer.Position = 0;
        }
    }

    private static Dictionary<string, string> BuildColumnMap(IDictionary<string, object?> headerRow)
    {
        var columns = new Dictionary<string, string>(StringComparer.Ordinal);
        var duplicates = new List<string>();

        foreach (var (cell, value) in headerRow)
        {
            var normalizedHeader = NormalizeHeader(ToText(value));
            if (normalizedHeader.Length == 0 || !HeaderLookup.TryGetValue(normalizedHeader, out var field))
                continue;

            if (!columns.TryAdd(field, cell))
                duplicates.Add(field);
        }

        var missing = RequiredFields
            .Where(field => !columns.ContainsKey(field))
            .ToList();

        if (missing.Count > 0 || duplicates.Count > 0)
        {
            throw new BusinessRuleException(
                "Excel başlıkları şablonla uyuşmuyor. Lütfen şablonu yeniden indirip kullanın.",
                "INVALID_EXCEL_HEADERS",
                new
                {
                    missingHeaders = missing,
                    duplicateHeaders = duplicates
                });
        }

        return columns;
    }

    private static Dictionary<string, string> BuildHeaderLookup()
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (field, aliases) in HeaderAliases)
        {
            foreach (var alias in aliases)
                lookup[NormalizeHeader(alias)] = field;
        }

        return lookup;
    }

    private static string NormalizeHeader(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var trimmed = value.Trim();
        if (trimmed.EndsWith('*'))
            trimmed = trimmed[..^1].Trim();

        var parts = trimmed.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts);
    }

    private static object? GetCell(
        IDictionary<string, object?> row,
        IReadOnlyDictionary<string, string> columns,
        string field) =>
        columns.TryGetValue(field, out var cell) && row.TryGetValue(cell, out var value)
            ? value
            : null;

    private static bool IsEmptyRow(IDictionary<string, object?> row) =>
        row.Values.All(value => string.IsNullOrWhiteSpace(ToText(value)));

    private static string? ToText(object? value)
    {
        if (value is null)
            return null;

        var text = Convert.ToString(value, CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static bool TryReadThreshold(object? value, out int threshold, out string? error)
    {
        threshold = DefaultMinStockThreshold;
        error = null;
        const string thresholdError = "Kritik stok eşiği 0 veya daha büyük bir tam sayı olmalıdır.";

        if (value is null || string.IsNullOrWhiteSpace(ToText(value)))
            return true;

        if (value is double doubleValue)
        {
            if (double.IsNaN(doubleValue) || double.IsInfinity(doubleValue) ||
                doubleValue != Math.Truncate(doubleValue) ||
                doubleValue < 0 || doubleValue > int.MaxValue)
            {
                error = thresholdError;
                return false;
            }

            threshold = (int)doubleValue;
            return true;
        }

        decimal numericValue;
        if (value is int intValue)
            numericValue = intValue;
        else if (value is long longValue)
            numericValue = longValue;
        else if (value is decimal decimalValue)
            numericValue = decimalValue;
        else if (value is float floatValue)
            numericValue = (decimal)floatValue;
        else if (!decimal.TryParse(ToText(value), NumberStyles.Number, CultureInfo.InvariantCulture, out numericValue) &&
                 !decimal.TryParse(ToText(value), NumberStyles.Number, CultureInfo.CurrentCulture, out numericValue))
        {
            error = thresholdError;
            return false;
        }

        if (numericValue != decimal.Truncate(numericValue) ||
            numericValue < 0 ||
            numericValue > int.MaxValue)
        {
            error = thresholdError;
            return false;
        }

        threshold = (int)numericValue;
        return true;
    }
}
