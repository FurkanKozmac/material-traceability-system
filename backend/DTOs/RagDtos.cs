using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

public record SafetyQuestionRequest(
    [Required(ErrorMessage = "Kimyasal kodu zorunludur!")]
    string ChemicalCode,

    [Required(ErrorMessage = "Soru metni boş olamaz!")]
    string Question
);

public record SafetyAnswerResponse(
    string ChemicalCode,
    string Question,
    string Answer,
    string RelevantSectionTitle,
    bool Success
);
