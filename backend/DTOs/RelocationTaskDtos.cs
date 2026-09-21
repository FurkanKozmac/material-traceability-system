using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

public record RelocationTaskResponse(
    long Id,
    string Status,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    long UnitId,
    string UnitBarcode,
    long FromAddressId,
    string FromAddressCode,
    long ToAddressId,
    string ToAddressCode,
    long? CompletedByUserId,
    string? CompletedByUsername
);

public record CreateRelocationTaskRequest(
    [Required] long UnitId,
    [Required] long ToAddressId
);

public record CompleteRelocationTaskRequest(
    [Required] string UnitBarcode,
    [Required] string TargetAddressBarcode
);

public record RelocationTargetResponse(
    long Id,
    string Code,
    string StorageType,
    int? MaxCapacity,
    int CurrentOccupancy,
    int? AvailableCapacity
);
