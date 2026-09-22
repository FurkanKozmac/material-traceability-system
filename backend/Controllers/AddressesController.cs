using backend.DTOs;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace backend.Controllers;

[ApiController]
[Route("api/addresses")]
public class AddressesController(IAddressService addressService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<AddressResponse>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] int? page,
        [FromQuery] int? size,
        CancellationToken ct) =>
        Ok(await addressService.GetAllAsync(page ?? 1, size ?? 20, ct));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AddressResponse>> GetById(long id, CancellationToken ct)
    {
        var address = await addressService.GetByIdAsync(id, ct);
        return address is null ? NotFound("Adres bulunamadı.") : Ok(address);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<AddressResponse>> Create(CreateAddressRequest request, CancellationToken ct)
    {
        try
        {
            var address = await addressService.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = address.Id }, address);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:long}/storage-type")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateStorageType(
        long id, UpdateAddressStorageTypeRequest request, CancellationToken ct)
    {
        var updated = await addressService.UpdateStorageTypeAsync(id, request.StorageType, ct);
        return updated ? NoContent() : NotFound("Raf bulunamadı.");
    }
}
