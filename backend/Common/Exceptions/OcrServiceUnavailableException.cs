namespace backend.Common.Exceptions;

public sealed class OcrServiceUnavailableException(
    string message,
    Exception? innerException = null,
    string errorCode = OcrServiceUnavailableException.UnavailableCode)
    : Exception(message, innerException)
{
    public const string UnavailableCode = "OCR_UNAVAILABLE";
    public const string DisabledCode = "OCR_DISABLED";

    public string ErrorCode { get; } = errorCode;
}
