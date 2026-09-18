using System.Globalization;
using System.Text.RegularExpressions;

namespace MiniPayroll.Domain.Billing;

public static class InvoiceNumbering
{
    private static readonly Regex Pattern = new(
        @"^INV-(\d{4})-(\d{6})$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static string Format(int year, int sequence) =>
        $"INV-{year}-{sequence:D6}";

    public static int NextSequence(IEnumerable<string> existingNumbers, int year)
    {
        var max = 0;
        foreach (var number in existingNumbers)
        {
            if (TryParse(number, out var parsedYear, out var sequence) && parsedYear == year)
            {
                max = Math.Max(max, sequence);
            }
        }

        return max + 1;
    }

    public static bool TryParse(string? number, out int year, out int sequence)
    {
        year = 0;
        sequence = 0;
        if (number is null)
        {
            return false;
        }

        var match = Pattern.Match(number);
        if (!match.Success)
        {
            return false;
        }

        year = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        sequence = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        return true;
    }
}
