namespace backend.Common.Exceptions;

public sealed class OcrServiceUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);
