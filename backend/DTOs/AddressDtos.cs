using System.ComponentModel.DataAnnotations;

namespace backend.DTOs;

public record AddressResponse(
    long Id,
    string Code,
    int MaxCapacity,
    int CurrentOccupancy,
    string StorageType
);

public record CreateAddressRequest(
    [Required] string Code,
    [Range(1, 1000)] int MaxCapacity,
    [Required] string StorageType
);

public record UpdateAddressStorageTypeRequest(
    [Required] string StorageType
);

public record UpdateAddressCapacityRequest(
    [Range(1, 1000)] int MaxCapacity
);
