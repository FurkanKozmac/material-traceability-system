using System.Text.RegularExpressions;
using backend.Common.Exceptions;

namespace backend.Common.Security;

public static class UsernamePolicy
{
    public const int MinimumLength = 3;
    public const int MaximumLength = 100;
    public const string AllowedCharactersPattern = @"^[A-Za-z0-9._-]+$";
    public const string AllowedCharactersErrorMessage =
        "Kullanıcı adı yalnızca harf, rakam, nokta, alt çizgi ve tire içerebilir.";

    private static readonly Regex AllowedCharacters = new(
        AllowedCharactersPattern,
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string NormalizeAndValidate(string? username)
    {
        var normalized = username?.Trim() ?? string.Empty;
        if (normalized.Length is < MinimumLength or > MaximumLength)
        {
            throw new BusinessRuleException(
                $"Kullanıcı adı {MinimumLength}-{MaximumLength} karakter olmalıdır.",
                "INVALID_USERNAME");
        }

        if (!AllowedCharacters.IsMatch(normalized))
            throw new BusinessRuleException(AllowedCharactersErrorMessage, "INVALID_USERNAME");

        return normalized;
    }
}
