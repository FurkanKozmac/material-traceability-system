using System.Security.Claims;
using backend.DTOs;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = "Admin")]
public class UsersController(IUserService userService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<UserResponse>>> GetAll(CancellationToken ct) =>
        Ok(await userService.GetAllAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<UserResponse>> GetById(long id, CancellationToken ct) =>
        Ok(await userService.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create(
        CreateUserRequest request, CancellationToken ct)
    {
        var user = await userService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }

    [HttpPut("{id:long}/role")]
    public async Task<ActionResult<UserResponse>> UpdateRole(
        long id, UpdateUserRoleRequest request, CancellationToken ct) =>
        Ok(await userService.UpdateRoleAsync(id, request, ct));

    [HttpPut("{id:long}/active")]
    public async Task<ActionResult<UserResponse>> UpdateActive(
        long id, UpdateUserActiveRequest request, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
            return Unauthorized();

        return Ok(await userService.UpdateActiveAsync(id, request, currentUserId.Value, ct));
    }

    [HttpPost("{id:long}/reset-password")]
    public async Task<IActionResult> ResetPassword(
        long id, ResetUserPasswordRequest request, CancellationToken ct)
    {
        await userService.ResetPasswordAsync(id, request, ct);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        var currentUserId = GetCurrentUserId();
        if (currentUserId is null)
            return Unauthorized();

        await userService.DeleteAsync(id, currentUserId.Value, ct);
        return NoContent();
    }

    private long? GetCurrentUserId() =>
        long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;
}
