using MiniPayroll.Domain.Entities;

namespace MiniPayroll.Domain.Payroll;

public static class CompanyAddressFormatter
{
    public const int MaxLength = 800;

    public static string? Format(Company company)
    {
        ArgumentNullException.ThrowIfNull(company);

        var formatted = string.Join(
            ", ",
            new[]
            {
                company.AddressLine1,
                company.AddressLine2,
                company.City,
                company.State,
                company.PostalCode
            }
            .Select(part => part?.Trim())
            .Where(part => !string.IsNullOrEmpty(part)));

        if (formatted.Length == 0)
        {
            return null;
        }

        return formatted.Length <= MaxLength ? formatted : formatted[..MaxLength];
    }
}
