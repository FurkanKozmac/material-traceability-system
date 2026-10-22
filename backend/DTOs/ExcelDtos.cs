using MiniExcelLibs.Attributes;

namespace backend.DTOs;

public record BulkImportResultDto(
    int TotalRows,
    int SuccessCount,
    int FailedCount,
    List<ExcelRowErrorDto> Errors
);

public record ExcelRowErrorDto(
    int RowNumber,
    string Field,
    string Message,
    string? Value = null
);

internal sealed class ChemicalTemplateRow
{
    [ExcelColumn(Name = "Kimyasal Kodu *", Index = 0)]
    public string ChemicalCode { get; set; } = string.Empty;

    [ExcelColumn(Name = "Kimyasal Adı *", Index = 1)]
    public string Name { get; set; } = string.Empty;

    [ExcelColumn(Name = "Depolama Tipi *", Index = 2)]
    public string StorageType { get; set; } = string.Empty;

    [ExcelColumn(Name = "Kritik Stok Eşiği", Index = 3)]
    public int? MinStockThreshold { get; set; }

    [ExcelColumn(Name = "Açıklama / İSG Notu", Index = 4)]
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
