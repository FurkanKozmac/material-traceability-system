using backend.Common.Exceptions;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/chemicals")]
public class ChemicalExcelController(IChemicalExcelService chemicalExcelService) : ControllerBase
{
    private const long MaxUploadBytes = 10 * 1024 * 1024;
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet("excel-template")]
    [Produces(XlsxContentType)]
    public async Task<IActionResult> DownloadTemplate(CancellationToken ct)
    {
        var content = await chemicalExcelService.CreateTemplateAsync(ct);
        return File(content, XlsxContentType, "kimyasal-toplu-yukleme-sablonu.xlsx");
    }

    [HttpPost("bulk-import")]
    [Authorize(Roles = "Admin")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    public async Task<IActionResult> BulkImport([FromForm] IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            throw new BusinessRuleException("Excel dosyası gereklidir.", "EXCEL_FILE_REQUIRED");

        if (file.Length > MaxUploadBytes)
            throw new BusinessRuleException("Excel dosyası en fazla 10 MB olabilir.", "EXCEL_FILE_TOO_LARGE");

        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException(
                "Yalnızca .xlsx biçimindeki Excel dosyaları yüklenebilir.",
                "INVALID_EXCEL_FILE");
        }

        await using var stream = file.OpenReadStream();
        var result = await chemicalExcelService.ImportAsync(stream, ct);
        return Ok(result);
    }
}
