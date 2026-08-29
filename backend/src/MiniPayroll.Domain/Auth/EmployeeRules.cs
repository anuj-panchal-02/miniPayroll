using System.Net.Mail;
using System.Text.RegularExpressions;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Auth;

public static class EmployeeRules
{
    public const int EmployeeCodeMinLength = 2;
    public const int EmployeeCodeMaxLength = 32;
    public const int NameMaxLength = 200;
    public const int EmailMaxLength = 256;
    public const int PhoneMaxLength = 30;
    public const int AddressLineMaxLength = 200;
    public const int CityMaxLength = 100;
    public const int StateMaxLength = 100;
    public const int PostalCodeMaxLength = 20;
    public const int DesignationMaxLength = 100;
    public const int DepartmentMaxLength = 100;
    public const int BankNameMaxLength = 100;
    public const int BankAccountMinLength = 9;
    public const int BankAccountMaxLength = 18;
    public const int IfscLength = 11;
    public const int UpiMaxLength = 100;

    private static readonly Regex EmployeeCodePattern = new(
        @"^[A-Za-z0-9-]+$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex IfscPattern = new(
        @"^[A-Z]{4}0[A-Z0-9]{6}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static void Normalize(Employee employee)
    {
        ArgumentNullException.ThrowIfNull(employee);

        employee.EmployeeCode = employee.EmployeeCode?.Trim() ?? string.Empty;
        employee.FullName = employee.FullName?.Trim() ?? string.Empty;
        employee.Phone = employee.Phone?.Trim() ?? string.Empty;
        employee.Email = employee.Email?.Trim() ?? string.Empty;
        employee.AddressLine1 = employee.AddressLine1?.Trim() ?? string.Empty;
        employee.AddressLine2 = TrimToNull(employee.AddressLine2);
        employee.City = employee.City?.Trim() ?? string.Empty;
        employee.State = employee.State?.Trim() ?? string.Empty;
        employee.PostalCode = employee.PostalCode?.Trim() ?? string.Empty;
        employee.Designation = employee.Designation?.Trim() ?? string.Empty;
        employee.Department = TrimToNull(employee.Department);
        employee.BankName = employee.BankName?.Trim() ?? string.Empty;
        employee.BankAccountNumber = DigitsOnly(employee.BankAccountNumber);
        employee.Ifsc = employee.Ifsc?.Trim().ToUpperInvariant() ?? string.Empty;
        employee.UpiId = TrimToNull(employee.UpiId);
        employee.EmploymentType = EmploymentType.FullTimeMonthly;
    }

    public static bool IsValid(Employee employee)
    {
        ArgumentNullException.ThrowIfNull(employee);

        return IsValidEmployeeCode(employee.EmployeeCode)
            && IsRequiredWithinLimit(employee.FullName, NameMaxLength)
            && IsValidEmail(employee.Email)
            && IsRequiredWithinLimit(employee.Phone, PhoneMaxLength)
            && IsRequiredWithinLimit(employee.AddressLine1, AddressLineMaxLength)
            && IsOptionalWithinLimit(employee.AddressLine2, AddressLineMaxLength)
            && IsRequiredWithinLimit(employee.City, CityMaxLength)
            && IsRequiredWithinLimit(employee.State, StateMaxLength)
            && IsRequiredWithinLimit(employee.PostalCode, PostalCodeMaxLength)
            && IsRequiredWithinLimit(employee.Designation, DesignationMaxLength)
            && IsOptionalWithinLimit(employee.Department, DepartmentMaxLength)
            && employee.EmploymentType == EmploymentType.FullTimeMonthly
            && employee.Status is EmployeeStatus.Active or EmployeeStatus.Inactive
            && employee.JoiningDate is { } joining
            && (employee.ExitDate is null || employee.ExitDate >= joining)
            && IsRequiredWithinLimit(employee.BankName, BankNameMaxLength)
            && IsValidAccountNumber(employee.BankAccountNumber)
            && IfscPattern.IsMatch(employee.Ifsc)
            && IsOptionalWithinLimit(employee.UpiId, UpiMaxLength)
            && (employee.OvertimeRate is null or >= 0);
    }

    public static bool IsValidDraft(Employee employee)
    {
        ArgumentNullException.ThrowIfNull(employee);

        return employee.Status == EmployeeStatus.Draft
            && IsValidEmployeeCode(employee.EmployeeCode)
            && IsRequiredWithinLimit(employee.FullName, NameMaxLength)
            && employee.EmploymentType == EmploymentType.FullTimeMonthly
            && IsOptionalDraftStep(employee.DraftStep)
            && IsOptionalEmail(employee.Email)
            && IsOptionalWithinLimit(employee.Phone, PhoneMaxLength)
            && IsOptionalWithinLimit(employee.AddressLine1, AddressLineMaxLength)
            && IsOptionalWithinLimit(employee.AddressLine2, AddressLineMaxLength)
            && IsOptionalWithinLimit(employee.City, CityMaxLength)
            && IsOptionalWithinLimit(employee.State, StateMaxLength)
            && IsOptionalWithinLimit(employee.PostalCode, PostalCodeMaxLength)
            && IsOptionalWithinLimit(employee.Designation, DesignationMaxLength)
            && IsOptionalWithinLimit(employee.Department, DepartmentMaxLength)
            && IsOptionalWithinLimit(employee.BankName, BankNameMaxLength)
            && IsOptionalAccountNumber(employee.BankAccountNumber)
            && IsOptionalIfsc(employee.Ifsc)
            && IsOptionalWithinLimit(employee.UpiId, UpiMaxLength)
            && (employee.JoiningDate is null
                || employee.ExitDate is null
                || employee.ExitDate >= employee.JoiningDate)
            && (employee.OvertimeRate is null or >= 0);
    }

    public static int ClampDraftStep(int? step) =>
        step is >= 1 and <= 4 ? step.Value : 1;

    public static string MaskAccountNumber(string? accountNumber)
    {
        var digits = DigitsOnly(accountNumber);
        if (digits.Length < 4)
        {
            return "****";
        }

        return $"****{digits[^4..]}";
    }

    public static bool IsValidEmployeeCode(string? employeeCode)
    {
        var value = employeeCode?.Trim() ?? string.Empty;
        return value.Length is >= EmployeeCodeMinLength and <= EmployeeCodeMaxLength
            && EmployeeCodePattern.IsMatch(value);
    }

    private static bool IsValidAccountNumber(string? accountNumber)
    {
        var digits = DigitsOnly(accountNumber);
        return digits.Length is >= BankAccountMinLength and <= BankAccountMaxLength;
    }

    private static bool IsValidEmail(string? email)
    {
        return IsRequiredWithinLimit(email, EmailMaxLength)
            && MailAddress.TryCreate(email, out var parsed)
            && string.Equals(parsed.Address, email, StringComparison.OrdinalIgnoreCase);
    }

    private static string DigitsOnly(string? value) =>
        new(value?.Where(char.IsDigit).ToArray() ?? []);

    private static string? TrimToNull(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static bool IsOptionalEmail(string? email) =>
        string.IsNullOrEmpty(email) || IsValidEmail(email);

    private static bool IsOptionalAccountNumber(string? accountNumber)
    {
        var digits = DigitsOnly(accountNumber);
        return digits.Length == 0 || IsValidAccountNumber(digits);
    }

    private static bool IsOptionalIfsc(string? ifsc) =>
        string.IsNullOrEmpty(ifsc) || IfscPattern.IsMatch(ifsc);

    private static bool IsOptionalDraftStep(int? step) =>
        step is null or >= 1 and <= 4;

    private static bool IsRequiredWithinLimit(string? value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= maxLength;

    private static bool IsOptionalWithinLimit(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) || value.Length <= maxLength;
}
