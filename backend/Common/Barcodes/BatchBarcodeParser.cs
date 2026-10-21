namespace backend.Common.Barcodes;

public static class BatchBarcodeParser
{
    public const string Prefix = "BAT-";

    public static bool TryParse(string? barcode, out string batchNo)
    {
        var normalized = barcode?.Trim() ?? string.Empty;
        if (!normalized.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) ||
            normalized.Length == Prefix.Length)
        {
            batchNo = string.Empty;
            return false;
        }

        batchNo = normalized[Prefix.Length..].Trim();
        return batchNo.Length > 0;
    }
}
