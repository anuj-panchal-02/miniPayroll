using System.Net.Mail;
using MiniPayroll.Domain.Entities;

namespace MiniPayroll.Domain.Auth;

public static class CompanySetupRules
{
    public const int NameMaxLength = 200;
    public const int EmailMaxLength = 256;
    public const int PhoneMaxLength = 30;
    public const int AddressLineMaxLength = 200;
    public const int CityMaxLength = 100;
    public const int StateMaxLength = 100;
    public const int PostalCodeMaxLength = 20;
    public const int LogoPathMaxLength = 500;
    public const int WeeklyOffDaysMaxLength = 100;
    public const int EstablishmentCodeMaxLength = 50;

    private static readonly string[] CanonicalDayNames =
    [
        "Monday",
        "Tuesday",
        "Wednesday",
        "Thursday",
        "Friday",
        "Saturday",
        "Sunday"
    ];

    public static void NormalizeCompanyDetails(Company company)
    {
        ArgumentNullException.ThrowIfNull(company);

        company.Name = company.Name?.Trim() ?? string.Empty;
        company.ContactEmail = company.ContactEmail?.Trim() ?? string.Empty;
        company.ContactPhone = TrimToNull(company.ContactPhone);
        company.AddressLine1 = TrimToNull(company.AddressLine1);
        company.AddressLine2 = TrimToNull(company.AddressLine2);
        company.City = TrimToNull(company.City);
        company.State = TrimToNull(company.State);
        company.PostalCode = TrimToNull(company.PostalCode);
    }

    public static bool HasValidCompanyDetails(Company company)
    {
        ArgumentNullException.ThrowIfNull(company);

        var name = company.Name?.Trim();
        var email = company.ContactEmail?.Trim();
        var phone = company.ContactPhone?.Trim();
        var addressLine1 = company.AddressLine1?.Trim();
        var addressLine2 = company.AddressLine2?.Trim();
        var city = company.City?.Trim();
        var state = company.State?.Trim();
        var postalCode = company.PostalCode?.Trim();

        return IsRequiredWithinLimit(name, NameMaxLength)
            && IsValidEmail(email)
            && IsRequiredWithinLimit(phone, PhoneMaxLength)
            && IsRequiredWithinLimit(addressLine1, AddressLineMaxLength)
            && IsOptionalWithinLimit(addressLine2, AddressLineMaxLength)
            && IsRequiredWithinLimit(city, CityMaxLength)
            && IsRequiredWithinLimit(state, StateMaxLength)
            && IsRequiredWithinLimit(postalCode, PostalCodeMaxLength);
    }

    public static bool HasValidPayrollSettings(Company company)
    {
        ArgumentNullException.ThrowIfNull(company);

        if (company.WorkingDaysPerMonth is < 1 or > 31
            || !Enum.IsDefined(company.DailyRateMethod)
            || !IsOptionalWithinLimit(company.PfEstablishmentCode, EstablishmentCodeMaxLength)
            || !IsOptionalWithinLimit(company.EsiCode, EstablishmentCodeMaxLength))
        {
            return false;
        }

        var storedDays = company.WeeklyOffDays?.Trim();
        if (string.IsNullOrEmpty(storedDays)
            || storedDays.Length > WeeklyOffDaysMaxLength
            || !TryCanonicalizeWeeklyOffDays(storedDays.Split(','), out var canonicalDays))
        {
            return false;
        }

        return string.Equals(storedDays, canonicalDays, StringComparison.Ordinal);
    }

    public static bool TryCanonicalizeWeeklyOffDays(
        IEnumerable<string?>? weeklyOffDays,
        out string canonical)
    {
        canonical = string.Empty;
        if (weeklyOffDays is null)
        {
            return false;
        }

        var selectedDays = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var suppliedDay in weeklyOffDays)
        {
            var day = suppliedDay?.Trim();
            var canonicalDay = CanonicalDayNames.FirstOrDefault(
                candidate => string.Equals(candidate, day, StringComparison.OrdinalIgnoreCase));

            if (canonicalDay is null || !selectedDays.Add(canonicalDay))
            {
                return false;
            }
        }

        if (selectedDays.Count == 0)
        {
            return false;
        }

        canonical = string.Join(
            ',',
            CanonicalDayNames.Where(day => selectedDays.Contains(day)));
        return canonical.Length <= WeeklyOffDaysMaxLength;
    }

    public static bool CanComplete(Company company)
    {
        ArgumentNullException.ThrowIfNull(company);

        var logoPath = company.LogoPath?.Trim();
        return HasValidCompanyDetails(company)
            && HasValidPayrollSettings(company)
            && IsRequiredWithinLimit(logoPath, LogoPathMaxLength);
    }

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static bool IsValidEmail(string? email)
    {
        return IsRequiredWithinLimit(email, EmailMaxLength)
            && MailAddress.TryCreate(email, out var parsed)
            && string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRequiredWithinLimit(string? value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maxLength;

    private static bool IsOptionalWithinLimit(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength;
}
