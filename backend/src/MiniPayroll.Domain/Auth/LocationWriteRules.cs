using System.Text.RegularExpressions;

namespace MiniPayroll.Domain.Auth;

public static class LocationWriteRules
{
    public const int NameMaxLength = 100;
    public const int CodeMinLength = 2;
    public const int CodeMaxLength = 3;

    private static readonly Regex CodePattern = new(
        @"^[A-Z]{2,3}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string NormalizeName(string? value) => value?.Trim() ?? string.Empty;

    public static string NormalizeCode(string? value) =>
        value?.Trim().ToUpperInvariant() ?? string.Empty;

    public static bool IsValidName(string? value)
    {
        var name = NormalizeName(value);
        return name.Length is > 0 and <= NameMaxLength;
    }

    public static bool IsValidCode(string? value)
    {
        var code = NormalizeCode(value);
        return code.Length is >= CodeMinLength and <= CodeMaxLength
            && CodePattern.IsMatch(code);
    }

    public static bool IsUniqueAmong(string? value, IEnumerable<string> siblings)
    {
        ArgumentNullException.ThrowIfNull(siblings);

        var candidate = NormalizeName(value);
        if (candidate.Length == 0)
        {
            return false;
        }

        return siblings.All(sibling =>
            !string.Equals(NormalizeName(sibling), candidate, StringComparison.OrdinalIgnoreCase));
    }
}
