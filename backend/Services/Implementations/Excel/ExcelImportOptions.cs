namespace backend.Services.Implementations.Excel;

public sealed class ExcelImportOptions
{
    public const string SectionName = "ExcelImport";
    public long MaxFileBytes { get; set; } = 2 * 1024 * 1024;
    public int MaxRows { get; set; } = 1000;
    public long MaxUncompressedBytes { get; set; } = 50 * 1024 * 1024;
    public int MaxArchiveEntries { get; set; } = 100;
}
