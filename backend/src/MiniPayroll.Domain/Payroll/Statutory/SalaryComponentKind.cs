namespace MiniPayroll.Domain.Payroll.Statutory;

public enum SalaryComponentKind
{
    OtherEarning = 0,
    Basic = 1,
    Da = 2,
    Hra = 3,
    Conveyance = 4,
    Special = 5
}

public static class SalaryComponentKinds
{
    public static SalaryComponentKind FromName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return SalaryComponentKind.OtherEarning;
        }

        if (name.Equals("Basic Salary", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Basic", StringComparison.OrdinalIgnoreCase))
        {
            return SalaryComponentKind.Basic;
        }

        if (name.Equals("Dearness Allowance (DA)", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Dearness Allowance", StringComparison.OrdinalIgnoreCase)
            || name.Equals("DA", StringComparison.OrdinalIgnoreCase))
        {
            return SalaryComponentKind.Da;
        }

        if (name.Equals("HRA", StringComparison.OrdinalIgnoreCase)
            || name.Equals("House Rent Allowance", StringComparison.OrdinalIgnoreCase))
        {
            return SalaryComponentKind.Hra;
        }

        if (name.Equals("Conveyance Allowance", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Conveyance", StringComparison.OrdinalIgnoreCase))
        {
            return SalaryComponentKind.Conveyance;
        }

        if (name.Equals("Special Allowance", StringComparison.OrdinalIgnoreCase))
        {
            return SalaryComponentKind.Special;
        }

        return SalaryComponentKind.OtherEarning;
    }

    public static bool IsStatutoryAmountName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        return name.Contains("Provident Fund", StringComparison.OrdinalIgnoreCase)
            || name.Equals("PF", StringComparison.OrdinalIgnoreCase)
            || name.Equals("ESI", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Professional Tax", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Labour Welfare", StringComparison.OrdinalIgnoreCase)
            || name.Equals("LWF", StringComparison.OrdinalIgnoreCase)
            || name.Equals("PT", StringComparison.OrdinalIgnoreCase);
    }
}
