using backend.DTOs;
using backend.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SafetyAssistantController(IMsdsRagService ragService) : ControllerBase
{
    // POST: /api/safetyassistant/ask
    [HttpPost("ask")]
    [AllowAnonymous]
    public async Task<ActionResult<SafetyAnswerResponse>> Ask(
        [FromBody] SafetyQuestionRequest request,
        CancellationToken ct)
    {
        var response = await ragService.AskQuestionAsync(request, ct);
        return Ok(response);
    }
}
