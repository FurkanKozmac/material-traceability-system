using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using backend.Data;
using backend.DTOs;
using backend.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace backend.Services.Implementations;

public sealed class LabelParserService(MtsDbContext context) : ILabelParserService
{
    private static readonly Regex LotRegex = new(
        // Do not interpret the LOT segment inside a value such as TEST-LOT-41 as
        // a new marker; otherwise the match starts at LOT and returns only 41.
        @"(?<![A-Z0-9-])(?:L[O0]T|[O0]7|BATCH|PART[Iİ](?:\s*NO)?|P\s*\.\s*NO)\b\s*[:#=\-]?\s*([A-Z0-9][A-Z0-9._/-]{1,63})(?![A-Z0-9._/-])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DateRegex = new(
        @"(?<!\d)(?<iso>\d{4}[./-]\d{1,2}[./-]\d{1,2})(?!\d)|(?<!\d)(?<dayFirst>\d{1,2}[./-]\d{1,2}[./-]\d{2,4})(?!\d)|(?<!\d)(?<monthYear>\d{1,2}/\d{2})(?!\d)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ExpirationMarkerRegex = new(
        @"\b(?:EXP(?:IRY|IRATION)?|SKT|BBD|BEST\s+BEFORE)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public async Task<BatchLabelScanResultDto> ParseAsync(string rawText, CancellationToken ct = default)
    {
        rawText ??= string.Empty;
        var lot = LotRegex.Match(rawText).Groups[1].Value.Trim().TrimEnd('.', ',', ';');
        var expiration = ExtractExpiration(rawText);
        var chemical = await SuggestChemicalAsync(rawText, ct);

        return new BatchLabelScanResultDto(
            chemical?.Id,
            chemical?.Name,
            string.IsNullOrWhiteSpace(lot) ? null : lot,
            expiration,
            rawText,
            string.IsNullOrWhiteSpace(lot) ? string.Empty : $"BAT-{lot}");
    }

    private async Task<ChemicalCandidate?> SuggestChemicalAsync(string text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var chemicals = await context.Chemicals.AsNoTracking()
            .Where(chemical => !chemical.Stopped)
            .Select(chemical => new ChemicalCandidate(chemical.Id, chemical.Name, chemical.ChemicalCode))
            .ToListAsync(ct);

        if (chemicals.Count == 0)
            return null;

        var normalizedText = Normalize(text);
        var exact = chemicals
            .SelectMany(chemical => new[]
            {
                (Chemical: chemical, Value: Normalize(chemical.ChemicalCode)),
                (Chemical: chemical, Value: Normalize(chemical.Name))
            })
            .Where(candidate => candidate.Value.Length > 0 && ContainsPhrase(normalizedText, candidate.Value))
            .OrderByDescending(candidate => candidate.Value.Length)
            .Select(candidate => candidate.Chemical)
            .FirstOrDefault();
        if (exact is not null)
            return exact;

        var scored = chemicals
            .Select(chemical => (Chemical: chemical, Score: Math.Max(
                SimilarityToText(Normalize(chemical.Name), normalizedText),
                SimilarityToText(Normalize(chemical.ChemicalCode), normalizedText))))
            .OrderByDescending(candidate => candidate.Score)
            .ToList();

        if (scored.Count == 0 || scored[0].Score < 0.78)
            return null;
        if (scored.Count > 1 && Math.Abs(scored[0].Score - scored[1].Score) < 0.03)
            return null;

        return scored[0].Chemical;
    }

    private static DateTime? ExtractExpiration(string rawText)
    {
        var candidates = DateRegex.Matches(rawText)
            .Select(match => new
            {
                Value = ParseExpiration(match),
                match.Index,
                IsLabeled = ExpirationMarkerRegex.IsMatch(rawText[
                    Math.Max(0, match.Index - 40)..match.Index])
            })
            .Where(candidate => candidate.Value.HasValue)
            .OrderByDescending(candidate => candidate.Value!.Value.Date >= DateTime.UtcNow.Date)
            .ThenByDescending(candidate => candidate.IsLabeled)
            .ThenBy(candidate => candidate.Index)
            .ToList();

        return candidates.FirstOrDefault()?.Value;
    }

    private static DateTime? ParseExpiration(Match match)
    {
        var value = match.Value;
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (match.Groups["monthYear"].Success)
        {
            var parts = value.Split('/');
            if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var month) ||
                month is < 1 or > 12 ||
                !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var shortYear))
                return null;

            var year = 2000 + shortYear;
            return new DateTime(year, month, DateTime.DaysInMonth(year, month), 0, 0, 0, DateTimeKind.Utc);
        }

        var formats = match.Groups["iso"].Success
            ? new[] { "yyyy.M.d", "yyyy.MM.dd", "yyyy/M/d", "yyyy/MM/dd", "yyyy-M-d", "yyyy-MM-dd" }
            : new[]
        {
            "d.M.yyyy", "dd.MM.yyyy", "d/M/yyyy", "dd/MM/yyyy", "d-M-yyyy", "dd-MM-yyyy",
            "d.M.yy", "dd.MM.yy", "d/M/yy", "dd/MM/yy", "d-M-yy", "dd-MM-yy"
        };
        if (!DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsed))
            return null;

        return DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
    }

    private static double SimilarityToText(string candidate, string text)
    {
        if (candidate.Length < 4)
            return 0;

        var expectedTokens = candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var textTokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (textTokens.Length == 0)
            return 0;

        var best = 0d;
        var minWindow = Math.Max(1, expectedTokens.Length - 1);
        var maxWindow = Math.Min(textTokens.Length, expectedTokens.Length + 1);
        for (var windowSize = minWindow; windowSize <= maxWindow; windowSize++)
        {
            for (var start = 0; start + windowSize <= textTokens.Length; start++)
            {
                var phrase = string.Join(' ', textTokens.Skip(start).Take(windowSize));
                var distance = Levenshtein(candidate, phrase);
                best = Math.Max(best, 1d - (double)distance / Math.Max(candidate.Length, phrase.Length));
            }
        }
        var expectedWordTokens = expectedTokens.Where(token => token.Length >= 3).ToArray();
        if (expectedWordTokens.Length == 0)
            return best;

        var tokenScores = expectedWordTokens.Select(expected => textTokens
            .Where(token => token.Length >= 3)
            .Select(token =>
            {
                if (expected == token)
                    return 1d;
                if ((expected.Contains(token, StringComparison.Ordinal) || token.Contains(expected, StringComparison.Ordinal)) &&
                    Math.Min(expected.Length, token.Length) >= 4)
                    return 0.9d;

                var distance = Levenshtein(expected, token);
                return 1d - (double)distance / Math.Max(expected.Length, token.Length);
            })
            .DefaultIfEmpty(0d)
            .Max()).ToArray();
        var coverage = (double)tokenScores.Count(score => score >= 0.65) / tokenScores.Length;
        var tokenScore = tokenScores.Average() * 0.6 + coverage * 0.4;
        return Math.Max(best, tokenScore);
    }

    private static int Levenshtein(string left, string right)
    {
        var previous = Enumerable.Range(0, right.Length + 1).ToArray();
        var current = new int[right.Length + 1];
        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + (left[i - 1] == right[j - 1] ? 0 : 1));
            (previous, current) = (current, previous);
        }
        return previous[right.Length];
    }

    private static bool ContainsPhrase(string text, string phrase) =>
        Regex.IsMatch(text, $@"(?<![A-Z0-9]){Regex.Escape(phrase)}(?![A-Z0-9])", RegexOptions.CultureInvariant);

    private static string Normalize(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;
            builder.Append(char.IsLetterOrDigit(character) ? char.ToUpperInvariant(character) : ' ');
        }
        return Regex.Replace(builder.ToString().Normalize(NormalizationForm.FormC), @"\s+", " ").Trim();
    }

    private sealed record ChemicalCandidate(long Id, string Name, string ChemicalCode);
}
