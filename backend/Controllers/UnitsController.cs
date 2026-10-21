using backend.Common.Exceptions;
using backend.DTOs;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Authorization;

namespace backend.Controllers;

[ApiController]
[Route("api/units")]
public class UnitsController(IUnitService unitService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<UnitResponse>>> Search(
        [FromQuery] string? search, [FromQuery] long? addressId, [FromQuery] int size = 100,
        CancellationToken ct = default) =>
        Ok(await unitService.SearchAsync(search, addressId, size, ct));

    [HttpGet("barcode/{barcode}")]
    public async Task<ActionResult<UnitResponse>> GetByBarcode(string barcode, CancellationToken ct)
    {
        var unit = await unitService.GetByBarcodeAsync(barcode, ct);
        if (unit is null)
            return NotFound($"'{barcode}' barkodlu stok birimi bulunamadı.");

        return Ok(unit);
    }

    [HttpGet("address/{addressId:long}")]
    public async Task<ActionResult<PagedResult<UnitResponse>>> GetByAddress(
        long addressId, [FromQuery] int page = 1, [FromQuery] int size = 50, CancellationToken ct = default)
    {
        var units = await unitService.GetByAddressIdAsync(addressId, page, size, ct);
        return Ok(units);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<UnitResponse>> Create(CreateUnitRequest request, CancellationToken ct)
    {
        try
        {
            var unit = await unitService.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetByBarcode), new { barcode = unit.Barcode }, unit);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{barcode}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string barcode, CancellationToken ct)
    {
        try
        {
            return await unitService.DeleteAsync(barcode, ct) ? NoContent() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("{barcode}/consume")]
    [Authorize(Roles = "Admin,Operator")]
    public async Task<ActionResult<UnitResponse>> Consume(
        string barcode,
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ConsumeUnitRequest? request,
        CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.BatchBarcode))
            throw new BusinessRuleException("Parti barkodu zorunludur.", "BATCH_REQUIRED");

        try
        {
            var result = await unitService.ConsumeAsync(barcode, request.BatchBarcode, ct);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
