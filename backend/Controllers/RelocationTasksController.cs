using backend.DTOs;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Route("api/relocation-tasks")]
public class RelocationTasksController(IRelocationTaskService taskService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<RelocationTaskResponse>>> GetAll(
        [FromQuery] string? search,
        [FromQuery] int? page,
        [FromQuery] int? size,
        CancellationToken ct) =>
        Ok(await taskService.GetAllAsync(ct));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RelocationTaskResponse>> Create(
        CreateRelocationTaskRequest request, CancellationToken ct)
    {
        try
        {
            return Ok(await taskService.CreateTaskAsync(request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("eligible-targets/{unitId:long}")]
    public async Task<ActionResult<List<RelocationTargetResponse>>> GetEligibleTargets(
        long unitId, CancellationToken ct) =>
        Ok(await taskService.GetEligibleTargetsAsync(unitId, ct));

    [HttpPut("{id:long}/complete")]
    [Authorize(Roles = "Admin,Operator")]
    public async Task<IActionResult> Complete(
        long id, CompleteRelocationTaskRequest request, CancellationToken ct)
    {
        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userId = long.TryParse(userIdValue, out var parsedUserId) ? parsedUserId : (long?)null;
        var success = await taskService.CompleteTaskAsync(id, request, userId, ct);
        return success ? NoContent() : NotFound("Görev bulunamadı veya zaten tamamlanmış.");
    }

    [HttpPut("{id:long}/cancel")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct)
    {
        var success = await taskService.CancelTaskAsync(id, ct);
        return success ? NoContent() : NotFound("Görev bulunamadı veya artık iptal edilemez.");
    }
}
