namespace backend.DTOs;

public sealed record BatchLabelScanResultDto(
    long? SuggestedChemicalId,
    string? SuggestedChemicalName,
    string? ExtractedLotNumber,
    DateTime? ExtractedExpirationDate,
    string RawText,
    string SuggestedBarcodePayload);
