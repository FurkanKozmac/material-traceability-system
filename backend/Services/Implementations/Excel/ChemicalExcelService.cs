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
using Microsoft.Extensions.Options;
using MiniExcelLibs;
using MiniExcelLibs.OpenXml;

namespace backend.Services.Implementations.Excel;

public sealed class ChemicalExcelService(MtsDbContext context, IOptions<ExcelImportOptions> options) : IChemicalExcelService
{
    private const int MaxChemicalCodeLength = 50;
    private const int MaxChemicalNameLength = 200;
    private const int MaxDescriptionLength = 2000;
    private const int MaxMinStockThreshold = 1_000_000;
    private const int DefaultMinStockThreshold = 10;

    private const string DataSheetName = "Kimyasallar";
    private const string GuideSheetName = "Kılavuz";
    private const string CodeField = ChemicalExcelColumns.Code;
    private const string NameField = ChemicalExcelColumns.Name;
    private const string StorageField = ChemicalExcelColumns.StorageType;
    private const string ThresholdField = ChemicalExcelColumns.MinStockThreshold;
    private const string DescriptionField = ChemicalExcelColumns.Description;
    private const string StorageTypeList = "GENERAL, SOLVENT, PAINT, FLAMMABLE, CORROSIVE";

    private const string ContentTypesEntryName = "[Content_Types].xml";
    private const string XlsxMainContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml";
    private static readonly byte[] ZipLocalFileHeader = [0x50, 0x4B, 0x03, 0x04];

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

    private ExcelImportOptions Limits => options.Value;

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
            new() { Topic = "Kritik stok eşiği", Description = $"Boş bırakılırsa 10 kabul edilir. 0 ile {MaxMinStockThreshold:N0} arasında bir tam sayı olmalıdır." },
            new() { Topic = "Uzunluk sınırları", Description = $"Kod en fazla {MaxChemicalCodeLength}, ad en fazla {MaxChemicalNameLength}, açıklama en fazla {MaxDescriptionLength} karakter." },
            new() { Topic = "Kayıtlı kodlar", Description = "Sistemde zaten kayıtlı kodlar güncellenmez; \"atlandı\" olarak raporlanır." },
            new() { Topic = "Hep ya hiç", Description = "Herhangi bir satırda hata varsa hiçbir satır kaydedilmez. Önce önizleme ile kontrol edin." },
            new() { Topic = "Örnek satır", Description = "Kimyasallar sayfasındaki örnek satırı içe aktarmadan önce silin veya kendi verinizle değiştirin." },
            new() { Topic = "Dosya biçimi", Description = $"Yalnızca .xlsx kabul edilir (.xlsm, .xls ve .csv reddedilir). En fazla {FormatMegabytes(Limits.MaxFileBytes)} MB ve {Limits.MaxRows:N0} veri satırı işlenir. Formüller çalıştırılmaz." }
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

    public async Task<BulkImportResultDto> ImportAsync(Stream stream, bool dryRun = false, CancellationToken ct = default)
    {
        await using var buffer = await ReadBoundedAsync(stream, ct);

        try
        {
            ValidatePackage(buffer);
            var sheetName = ResolveDataSheet(buffer);
            return await ReadAndImportRowsAsync(buffer, sheetName, dryRun, ct);
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

    private async Task<MemoryStream> ReadBoundedAsync(Stream stream, CancellationToken ct)
    {
        var maxBytes = Limits.MaxFileBytes;
        var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;

        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > maxBytes)
            {
                await buffer.DisposeAsync();
                throw new BusinessRuleException(
                    $"Excel dosyası en fazla {FormatMegabytes(maxBytes)} MB olabilir.",
                    "EXCEL_FILE_TOO_LARGE");
            }

            buffer.Write(chunk, 0, read);
        }

        if (buffer.Length == 0)
        {
            await buffer.DisposeAsync();
            throw new BusinessRuleException("Excel dosyası gereklidir.", "EXCEL_FILE_REQUIRED");
        }

        buffer.Position = 0;
        return buffer;
    }

    private async Task<BulkImportResultDto> ReadAndImportRowsAsync(
        MemoryStream buffer,
        string sheetName,
        bool dryRun,
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
        var resultRows = new List<ExcelImportRowDto>();
        var validChemicals = new List<Chemical>();
        Dictionary<string, string>? columns = null;
        var totalRows = 0;
        var skippedCount = 0;
        var failedCount = 0;
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
            if (totalRows > Limits.MaxRows)
            {
                throw new BusinessRuleException(
                    $"Bir seferde en fazla {Limits.MaxRows:N0} satır içe aktarılabilir.",
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

            if (description is { Length: > MaxDescriptionLength })
            {
                AddError(DescriptionField,
                    $"Açıklama en fazla {MaxDescriptionLength} karakter olabilir.");
            }

            string status;
            if (rowHasError)
            {
                failedCount++;
                status = ExcelImportRowStatus.Error;
            }
            else if (existingCodes.Contains(code!))
            {
                // Existing chemicals are never updated (no upsert); re-importing the same file is idempotent.
                skippedCount++;
                status = ExcelImportRowStatus.Skip;
            }
            else
            {
                status = ExcelImportRowStatus.Insert;
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

            resultRows.Add(new ExcelImportRowDto(rowNumber, code, name, status));
        }

        if (columns is null || totalRows == 0)
        {
            throw new BusinessRuleException(
                "Excel dosyasında içe aktarılacak veri satırı bulunamadı.",
                "EMPTY_EXCEL_FILE");
        }

        // All-or-nothing: a single invalid row means nothing is written.
        var commit = !dryRun && failedCount == 0;
        if (commit && validChemicals.Count > 0)
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
                context.ChangeTracker.Clear();
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
            dryRun,
            commit,
            totalRows,
            validChemicals.Count,
            skippedCount,
            failedCount,
            resultRows,
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

    /// <summary>
    /// Verifies the upload is a macro-free .xlsx package without trusting the file name:
    /// ZIP signature, OOXML spreadsheet content type, no VBA project, and bounded decompression.
    /// Zip-bomb guard: entry count and the sum of declared uncompressed sizes are checked from the central
    /// directory headers only, before any entry is opened. ZipArchive never inflates an entry past its
    /// declared length (covered by a test), so a forged header cannot expand beyond the cap.
    /// </summary>
    private void ValidatePackage(MemoryStream buffer)
    {
        if (!HasZipSignature(buffer))
        {
            throw new BusinessRuleException(
                "Dosya geçerli bir .xlsx çalışma kitabı değil. .xls, .csv veya yeniden adlandırılmış dosyalar kabul edilmez.",
                "INVALID_EXCEL_FILE");
        }

        buffer.Position = 0;
        try
        {
            using var archive = new ZipArchive(buffer, ZipArchiveMode.Read, leaveOpen: true);

            if (archive.Entries.Count > Limits.MaxArchiveEntries)
                throw UncompressedLimitExceeded();

            long declaredBytes = 0;
            foreach (var entry in archive.Entries)
            {
                if (entry.Length > Limits.MaxUncompressedBytes - declaredBytes)
                    throw UncompressedLimitExceeded();

                declaredBytes += entry.Length;

                if (entry.FullName.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase))
                    throw MacroFileNotAllowed();
            }

            ValidateContentTypes(archive);
        }
        finally
        {
            buffer.Position = 0;
        }
    }

    private static bool HasZipSignature(MemoryStream buffer)
    {
        if (buffer.Length < ZipLocalFileHeader.Length)
            return false;

        var bytes = buffer.GetBuffer();
        return bytes.AsSpan(0, ZipLocalFileHeader.Length).SequenceEqual(ZipLocalFileHeader);
    }

    private static void ValidateContentTypes(ZipArchive archive)
    {
        var entry = archive.GetEntry(ContentTypesEntryName)
            ?? throw new BusinessRuleException(
                "Dosya geçerli bir .xlsx çalışma kitabı değil.",
                "INVALID_EXCEL_FILE");

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 1024 * 1024
        };

        var hasSpreadsheetMainPart = false;
        using (var stream = entry.Open())
        using (var reader = XmlReader.Create(stream, settings))
        {
            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element)
                    continue;

                var contentType = reader.GetAttribute("ContentType");
                if (contentType is null)
                    continue;

                if (contentType.Contains("macroEnabled", StringComparison.OrdinalIgnoreCase) ||
                    contentType.Contains("vbaProject", StringComparison.OrdinalIgnoreCase))
                {
                    throw MacroFileNotAllowed();
                }

                if (string.Equals(contentType, XlsxMainContentType, StringComparison.OrdinalIgnoreCase))
                    hasSpreadsheetMainPart = true;
            }
        }

        if (!hasSpreadsheetMainPart)
        {
            throw new BusinessRuleException(
                "Dosya geçerli bir .xlsx çalışma kitabı değil.",
                "INVALID_EXCEL_FILE");
        }
    }

    private static BusinessRuleException UncompressedLimitExceeded() =>
        new("Excel dosyasının açılmış boyutu güvenli sınırı aşıyor.", "INVALID_EXCEL_FILE");

    private static BusinessRuleException MacroFileNotAllowed() =>
        new("Makro içeren Excel dosyaları (.xlsm) kabul edilmez. Lütfen .xlsx olarak kaydedin.", "MACRO_FILE_NOT_ALLOWED");

    private static string FormatMegabytes(long bytes) =>
        (bytes / (1024d * 1024d)).ToString("0.#", CultureInfo.InvariantCulture);

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
        var thresholdError = $"Kritik stok eşiği 0 ile {MaxMinStockThreshold:N0} arasında bir tam sayı olmalıdır.";

        if (value is null || string.IsNullOrWhiteSpace(ToText(value)))
            return true;

        if (value is double doubleValue)
        {
            if (double.IsNaN(doubleValue) || double.IsInfinity(doubleValue) ||
                doubleValue != Math.Truncate(doubleValue) ||
                doubleValue < 0 || doubleValue > MaxMinStockThreshold)
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
            numericValue > MaxMinStockThreshold)
        {
            error = thresholdError;
            return false;
        }

        threshold = (int)numericValue;
        return true;
    }
}
