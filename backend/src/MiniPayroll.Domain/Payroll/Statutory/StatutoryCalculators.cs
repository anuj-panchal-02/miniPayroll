using MiniPayroll.Domain.Payroll;

namespace MiniPayroll.Domain.Payroll.Statutory;

public static class PfCalculator
{
    public static (decimal Employee, decimal Employer) Calculate(
        decimal pfWages,
        bool covered,
        bool useWageCeiling,
        DateOnly on,
        IStatutoryRuleProvider? rules = null)
    {
        if (!covered || pfWages <= 0)
        {
            return (0m, 0m);
        }

        var rule = (rules ?? ConfiguredStatutoryRuleProvider.Instance).PfFor(on);
        var wages = useWageCeiling ? Math.Min(pfWages, rule.WageCeiling) : pfWages;
        return (PayrollMoney.Rupees(wages * rule.EmployeeRate), PayrollMoney.Rupees(wages * rule.EmployerRate));
    }
}

public static class EsiCalculator
{
    public static (decimal Employee, decimal Employer) Calculate(
        decimal esiWages,
        bool covered,
        DateOnly on,
        IStatutoryRuleProvider? rules = null)
    {
        if (!covered || esiWages <= 0)
        {
            return (0m, 0m);
        }

        var rule = (rules ?? ConfiguredStatutoryRuleProvider.Instance).EsiFor(on);
        return (PayrollMoney.Rupees(esiWages * rule.EmployeeRate), PayrollMoney.Rupees(esiWages * rule.EmployerRate));
    }
}

public static class ProfessionalTaxCalculator
{
    public static decimal Calculate(
        string? companyState,
        Enums.Gender? gender,
        decimal salary,
        int month,
        DateOnly? on = null,
        IStatutoryRuleProvider? rules = null) =>
        (rules ?? ConfiguredStatutoryRuleProvider.Instance).ProfessionalTax(
            companyState,
            gender,
            salary,
            month,
            on ?? DateOnly.MaxValue);
}

public static class LwfCalculator
{
    public static decimal Calculate(
        string? companyState,
        int month,
        DateOnly? on = null,
        IStatutoryRuleProvider? rules = null) =>
        (rules ?? ConfiguredStatutoryRuleProvider.Instance).LabourWelfareFund(
            companyState,
            month,
            on ?? DateOnly.MaxValue);
}
