namespace MiniPayroll.Domain.Payroll.Statutory;

public static class PfCalculator
{
    public static (decimal Employee, decimal Employer) Calculate(
        decimal pfWages,
        bool covered,
        bool useWageCeiling,
        DateOnly on)
    {
        if (!covered || pfWages <= 0)
        {
            return (0m, 0m);
        }

        var rule = PfRules.For(on);
        var wages = useWageCeiling ? Math.Min(pfWages, rule.WageCeiling) : pfWages;
        return (Rupees(wages * rule.EmployeeRate), Rupees(wages * rule.EmployerRate));
    }

    private static decimal Rupees(decimal amount) =>
        decimal.Round(amount, 0, MidpointRounding.AwayFromZero);
}

public static class EsiCalculator
{
    public static (decimal Employee, decimal Employer) Calculate(
        decimal esiWages,
        bool covered,
        DateOnly on)
    {
        if (!covered || esiWages <= 0)
        {
            return (0m, 0m);
        }

        var rule = EsiRules.For(on);
        return (Rupees(esiWages * rule.EmployeeRate), Rupees(esiWages * rule.EmployerRate));
    }

    private static decimal Rupees(decimal amount) =>
        decimal.Round(amount, 0, MidpointRounding.AwayFromZero);
}

public static class ProfessionalTaxCalculator
{
    public static decimal Calculate(
        string? companyState,
        Enums.Gender? gender,
        decimal salary,
        int month) =>
        ProfessionalTaxSlabs.AmountFor(companyState, gender, salary, month);
}

public static class LwfCalculator
{
    public static decimal Calculate(string? companyState, int month) =>
        LwfRules.AmountFor(companyState, month);
}
