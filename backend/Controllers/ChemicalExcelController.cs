using backend.Common.Exceptions;
using backend.Common.Security;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace backend.Controllers;

[ApiController]
[Route("api/chemicals")]
[Authorize(Roles = "Admin")]
public class ChemicalExcelController(IChemicalExcelService chemicalExcelService) : ControllerBase
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet("excel-template")]
    [Produces(XlsxContentType)]
    public async Task<IActionResult> DownloadTemplate(CancellationToken ct)
    {
        var content = await chemicalExcelService.CreateTemplateAsync(ct);
        return File(content, XlsxContentType, "kimyasal-toplu-yukleme-sablonu.xlsx");
    }

    /// <summary>
    /// Imports chemicals from an .xlsx file. With <c>dryRun=true</c> only validates and previews; nothing is written.
    /// A real import is all-or-nothing: if any row is invalid, no row is saved and 422 is returned with the report.
    /// </summary>
    [HttpPost("bulk-import")]
    [EnableRateLimiting("excel-import")]
    [Consumes("multipart/form-data")]
    [TypeFilter(typeof(ExcelUploadLimitsFilter))]
    public async Task<IActionResult> BulkImport(
        [FromForm] IFormFile? file,
        [FromQuery] bool dryRun = false,
        CancellationToken ct = default)
    {
        if (file is null || file.Length == 0)
            throw new BusinessRuleException("Excel dosyası gereklidir.", "EXCEL_FILE_REQUIRED");

        // The extension is only an early hint; the service decides by inspecting the file content.
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException(
                "Yalnızca .xlsx biçimindeki Excel dosyaları yüklenebilir.",
                "INVALID_EXCEL_FILE");
        }

        await using var stream = file.OpenReadStream();
        var result = await chemicalExcelService.ImportAsync(stream, dryRun, ct);

        return !dryRun && result.FailedCount > 0
            ? UnprocessableEntity(result)
            : Ok(result);
    }
}
