using System.Drawing;
using backend.Data;
using backend.DTOs;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using MiniExcelLibs;
using MiniExcelLibs.OpenXml;

namespace backend.Services.Implementations.Excel;

public sealed class InventoryReportService(MtsDbContext context) : IInventoryReportService
{
    private const string ExpiredStatus = "Süresi Geçmiş";
    private const string CriticalStatus = "Kritik Eşik Altı";
    private const string NormalStatus = "Normal";

    public async Task<byte[]> ExportAuditReportAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;

        var sourceRows = await context.Units
            .AsNoTracking()
            .Where(unit => unit.Status == "InStock" || unit.Status == "AVAILABLE")
            .GroupBy(unit => new
            {
                unit.BatchId,
                unit.Batch.BatchNo,
                unit.Batch.ChemicalId,
                unit.Batch.Chemical.ChemicalCode,
                unit.Batch.Chemical.Name,
                unit.Batch.Chemical.StorageType,
                unit.Batch.Chemical.MinStockThreshold,
                unit.Batch.ExpirationDate,
                AddressCode = unit.Address == null ? null : unit.Address.Code
            })
            .Select(group => new
            {
                BatchNo = group.Key.BatchNo,
                ChemicalId = group.Key.ChemicalId,
                ChemicalCode = group.Key.ChemicalCode,
                ChemicalName = group.Key.Name,
                StorageType = group.Key.StorageType,
                MinStockThreshold = group.Key.MinStockThreshold,
                ExpirationDate = group.Key.ExpirationDate,
                AddressCode = group.Key.AddressCode,
                Quantity = group.Count()
            })
            .ToListAsync(ct);

        var chemicalTotals = sourceRows
            .GroupBy(row => row.ChemicalId)
            .ToDictionary(group => group.Key, group => group.Sum(row => row.Quantity));

        var reportRows = sourceRows
            .Select(row => new InventoryReportRow
            {
                BatchNo = row.BatchNo,
                Chemical = $"{row.ChemicalCode} - {row.ChemicalName}",
                StorageType = row.StorageType,
                Address = row.AddressCode ?? "-",
                Quantity = row.Quantity,
                ExpirationDate = row.ExpirationDate,
                Status = DetermineStatus(
                    row.ExpirationDate,
                    chemicalTotals[row.ChemicalId],
                    row.MinStockThreshold,
                    today)
            })
            .OrderBy(row => StatusOrder(row.Status))
            .ThenBy(row => row.Chemical, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.BatchNo, StringComparer.OrdinalIgnoreCase)
            .ThenBy(row => row.Address, StringComparer.OrdinalIgnoreCase)
            .ToList();

        await using var stream = new MemoryStream();
        await stream.SaveAsAsync(
            reportRows,
            sheetName: "Envanter",
            configuration: CreateReportConfiguration(),
            cancellationToken: ct);

        return stream.ToArray();
    }

    private static string DetermineStatus(
        DateTime? expirationDate,
        int totalQuantity,
        int minStockThreshold,
        DateTime today)
    {
        if (expirationDate.HasValue && expirationDate.Value.Date < today)
            return ExpiredStatus;

        if (totalQuantity < minStockThreshold)
            return CriticalStatus;

        return NormalStatus;
    }

    private static int StatusOrder(string status) => status switch
    {
        ExpiredStatus => 0,
        CriticalStatus => 1,
        _ => 2
    };

    private static OpenXmlConfiguration CreateReportConfiguration() => new()
    {
        FastMode = true,
        EnableAutoWidth = true,
        MinWidth = 10,
        MaxWidth = 60,
        FreezeRowCount = 1,
        AutoFilter = true,
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
            WrapCellContents = false
        }
    };
}
