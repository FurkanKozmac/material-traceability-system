namespace backend.Common.Exceptions;

public class BusinessRuleException(
	string message,
	string? errorCode = null,
	object? details = null) : Exception(message)
{
	public string? ErrorCode { get; } = errorCode;
	public object? Details { get; } = details;
}
