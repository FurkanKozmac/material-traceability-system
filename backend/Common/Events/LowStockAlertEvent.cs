namespace backend.Common.Events;

public sealed record LowStockAlertEvent(
    int ChemicalId,
    string ChemicalName,
    string ChemicalCode,
    string BatchNumber,
    int RemainingStock,
    int MinThreshold,
    DateTime Timestamp);
