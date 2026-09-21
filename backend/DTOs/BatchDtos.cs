using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

public record BatchResponse(
    long Id,
    string BatchNo,
    string? Supplier,
    int InitialQuantity,
    DateTime? ExpirationDate,
    long ChemicalId,
    string ChemicalName,
    int UnitCount
);

public record CreateBatchRequest(
    [Required] string BatchNo,
    string? Supplier,
    [Range(1, 10000)] int InitialQuantity,
    DateTime? ExpirationDate,
    [Required] long ChemicalId
);
