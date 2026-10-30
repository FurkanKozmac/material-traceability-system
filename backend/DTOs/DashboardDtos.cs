namespace backend.DTOs;

public record NearExpiryBatchDto(
    long BatchId,
    string BatchNo,
    string ChemicalName,
    DateTime ExpirationDate,
    int DaysLeft,
    int ActiveUnits
);

public record LowStockChemicalDto(
    long ChemicalId,
    string ChemicalCode,
    string Name,
    int ActiveUnits,
    int MinStockThreshold
);

public record DashboardAlertsResponse(
    int NearExpiryDays,
    int NearExpiryTotal,
    List<NearExpiryBatchDto> NearExpiry,
    int LowStockTotal,
    List<LowStockChemicalDto> LowStock
);
