using MiniExcelLibs.Attributes;

namespace backend.DTOs;

public record BulkImportResultDto(
    bool DryRun,
    bool Committed,
    int TotalRows,
    int InsertCount,
    int SkippedCount,
    int FailedCount,
    List<ExcelImportRowDto> Rows,
    List<ExcelRowErrorDto> Errors
);

public record ExcelImportRowDto(
    int RowNumber,
    string? ChemicalCode,
    string? Name,
    string Status
);

public static class ExcelImportRowStatus
{
    public const string Insert = "Insert";
    public const string Skip = "Skip";
    public const string Error = "Error";
}

/// <summary>
/// Single source of the chemical sheet schema, shared by the template writer and the importer.
/// </summary>
public static class ChemicalExcelColumns
{
    public const string Code = "Kimyasal Kodu *";
    public const string Name = "Kimyasal Adı *";
    public const string StorageType = "Depolama Tipi *";
    public const string MinStockThreshold = "Kritik Stok Eşiği";
    public const string Description = "Açıklama / İSG Notu";

    public static readonly IReadOnlyList<string> All = [Code, Name, StorageType, MinStockThreshold, Description];
}

public record ExcelRowErrorDto(
    int RowNumber,
    string Field,
    string Message,
    string? Value = null
);

internal sealed class ChemicalTemplateRow
{
    [ExcelColumn(Name = ChemicalExcelColumns.Code, Index = 0)]
    public string ChemicalCode { get; set; } = string.Empty;

    [ExcelColumn(Name = ChemicalExcelColumns.Name, Index = 1)]
    public string Name { get; set; } = string.Empty;

    [ExcelColumn(Name = ChemicalExcelColumns.StorageType, Index = 2)]
    public string StorageType { get; set; } = string.Empty;

    [ExcelColumn(Name = ChemicalExcelColumns.MinStockThreshold, Index = 3)]
    public int? MinStockThreshold { get; set; }

    [ExcelColumn(Name = ChemicalExcelColumns.Description, Index = 4)]
    public string? Description { get; set; }
}

internal sealed class ChemicalTemplateGuideRow
{
    [ExcelColumn(Name = "Konu", Index = 0)]
    public string Topic { get; set; } = string.Empty;

    [ExcelColumn(Name = "Açıklama", Index = 1)]
    public string Description { get; set; } = string.Empty;
}

internal sealed class InventoryReportRow
{
    [ExcelColumn(Name = "Parti No (Batch No)", Index = 0)]
    public string BatchNo { get; set; } = string.Empty;

    [ExcelColumn(Name = "Kimyasal Kodu & Adı", Index = 1)]
    public string Chemical { get; set; } = string.Empty;

    [ExcelColumn(Name = "Depolama Tipi", Index = 2)]
    public string StorageType { get; set; } = string.Empty;

    [ExcelColumn(Name = "Bulunduğu Raf / Adres", Index = 3)]
    public string Address { get; set; } = string.Empty;

    [ExcelColumn(Name = "Mevcut Stok Miktarı", Index = 4)]
    public int Quantity { get; set; }

    [ExcelColumn(Name = "Son Kullanma Tarihi (SKT)", Index = 5, Format = "dd.MM.yyyy")]
    public DateTime? ExpirationDate { get; set; }

    [ExcelColumn(Name = "Durum", Index = 6)]
    public string Status { get; set; } = string.Empty;
}
