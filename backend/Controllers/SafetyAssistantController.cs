using backend.DTOs;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SafetyAssistantController(IMsdsRagService ragService) : ControllerBase
{
    // POST: /api/safetyassistant/ask
    [HttpPost("ask")]
    [AllowAnonymous]
    [EnableRateLimiting("safety-assistant")]
    public async Task<ActionResult<SafetyAnswerResponse>> Ask(
        [FromBody] SafetyQuestionRequest request,
        CancellationToken ct)
    {
        var response = await ragService.AskQuestionAsync(request, ct);
        return Ok(response);
    }
}
