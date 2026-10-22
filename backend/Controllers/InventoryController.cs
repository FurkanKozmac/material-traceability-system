using backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/inventory")]
public class InventoryController(IInventoryReportService inventoryReportService) : ControllerBase
{
    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet("export-excel")]
    [Authorize(Roles = "Admin,Operator")]
    [Produces(XlsxContentType)]
    public async Task<IActionResult> ExportExcel(CancellationToken ct)
    {
        var content = await inventoryReportService.ExportAuditReportAsync(ct);
        var fileName = $"envanter-denetim-raporu-{DateTime.UtcNow:yyyyMMdd-HHmm}.xlsx";
        return File(content, XlsxContentType, fileName);
    }
}
