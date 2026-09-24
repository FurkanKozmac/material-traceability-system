using backend.DTOs;

namespace backend.Services.Interfaces;

public interface IMsdsRagService
{
    Task<SafetyAnswerResponse> AskQuestionAsync(SafetyQuestionRequest request, CancellationToken ct = default);
}
