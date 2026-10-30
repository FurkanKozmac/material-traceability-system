namespace backend.Services.Implementations.Excel;

/// <summary>
/// Neutralizes spreadsheet formula injection (OWASP CSV injection) in user-supplied text cells.
/// </summary>
internal static class SpreadsheetCellEscaper
{
    private static readonly char[] DangerousPrefixes = ['=', '+', '-', '@', '\t', '\r'];

    public static string Escape(string value) =>
        value.Length > 0 && Array.IndexOf(DangerousPrefixes, value[0]) >= 0
            ? "'" + value
            : value;
}
