using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

public record UnitResponse(
    long Id,
    string Barcode,
    string Status,
    long? AddressId,
    string? AddressCode,
    long BatchId,
    string BatchNo,
    string ChemicalName,
    string ChemicalCode,
    DateTime? ExpirationDate,
    bool IsExpired
);

public record CreateUnitRequest(
    [Required] string Barcode,
    [Required] long BatchId,
    long? AddressId
);
