using backend.Data;
using backend.DTOs;
using backend.Entities;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/chemicals")]
public class ChemicalController(IChemicalService chemicalService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ChemicalResponse>>> GetAll(CancellationToken ct)
    {
        var result = await chemicalService.GetAllAsync(ct);

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ChemicalResponse>> GetById(long id, CancellationToken ct)
    {
        var result = await chemicalService.GetByIdAsync(id, ct);

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ChemicalResponse>> Create(CreateChemicalRequest request, CancellationToken ct)
    {
        try
        {
            var result = await chemicalService.CreateAsync(request, ct);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPut("{id:long}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(long id, UpdateChemicalRequest request, CancellationToken ct)
    {
        var success = await chemicalService.UpdateAsync(id, request, ct);
        if (!success) return NotFound("Chemical cannot be found to update.");

        return NoContent();
    }

    [HttpDelete("{id:long}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var success = await chemicalService.DeleteAsync(id, ct);
        if (!success) return NotFound("Chemical cannot be found to delete");

        return NoContent();
    }
}
