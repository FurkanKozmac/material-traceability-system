using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

public record ChemicalResponse(
    long Id,
    string Name,
    string ChemicalCode,
    string? Description,
    string? MsdsUrl,
    bool Stopped,
    string StorageType
);

public record CreateChemicalRequest(
    [Required(ErrorMessage = "Chemical name must be provided!")]
    string Name,
    [Required(ErrorMessage = "Chemical code must be provided!")]
    string ChemicalCode,
    string? Description,
    string? MsdsUrl,
    [Required] string StorageType
);

public record UpdateChemicalRequest(
    [Required(ErrorMessage = "Chemical name must be provided!")]
    string Name,
    string? Description,
    string? MsdsUrl,
    bool Stopped,
    [Required] string StorageType
);
