using backend.Common.Exceptions;

namespace backend.Common.Security;

public static class PasswordPolicy
{
    public const int MinimumLength = 12;
    public const int MaximumLength = 128;

    public static void Validate(string? newPassword, string? currentPassword = null)
    {
        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < MinimumLength)
        {
            throw new BusinessRuleException(
                $"Parola en az {MinimumLength} karakter olmalıdır.",
                "INVALID_PASSWORD");
        }

        if (newPassword.Length > MaximumLength)
        {
            throw new BusinessRuleException(
                $"Parola en fazla {MaximumLength} karakter olmalıdır.",
                "INVALID_PASSWORD");
        }

        if (currentPassword is not null &&
            string.Equals(newPassword, currentPassword, StringComparison.Ordinal))
        {
            throw new BusinessRuleException(
                "Yeni parola mevcut parolayla aynı olamaz.",
                "PASSWORD_UNCHANGED");
        }
    }
}
