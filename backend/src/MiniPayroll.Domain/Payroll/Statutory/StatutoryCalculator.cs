using MiniPayroll.Domain.Payroll;

namespace MiniPayroll.Domain.Payroll.Statutory;

public static class StatutoryCalculator
{
    public static IReadOnlyList<StatutoryLineResult> Calculate(
        StatutoryPolicy policy,
        DateOnly on,
        int month,
        decimal pfWages,
        decimal esiWages,
        decimal ptWages,
        IReadOnlyList<StatutoryOverride>? overrides,
        IStatutoryRuleProvider? rules = null)
    {
        var overrideByKind = (overrides ?? [])
            .GroupBy(item => item.Kind)
            .ToDictionary(group => group.Key, group => group.Last().Amount);

        var pfCovered = policy.PfApplicable && policy.PfCovered;
        var esiCovered = policy.EsiApplicable && policy.EsiCovered;
        var (pfEmployee, pfEmployer) = PfCalculator.Calculate(
            pfWages, pfCovered, policy.PfUseWageCeiling, on, rules);
        var (esiEmployee, esiEmployer) = EsiCalculator.Calculate(esiWages, esiCovered, on, rules);
        var pt = ProfessionalTaxCalculator.Calculate(
            policy.CompanyState, policy.Gender, ptWages, month, on, rules);
        var lwf = LwfCalculator.Calculate(policy.CompanyState, month, on, rules);

        return
        [
            Apply(StatutoryKind.PfEmployee, pfEmployee, pfEmployer, overrideByKind),
            Apply(StatutoryKind.EsiEmployee, esiEmployee, esiEmployer, overrideByKind),
            Apply(StatutoryKind.ProfessionalTax, pt, 0m, overrideByKind),
            Apply(StatutoryKind.LwfEmployee, lwf, 0m, overrideByKind)
        ];
    }

    private static StatutoryLineResult Apply(
        StatutoryKind kind,
        decimal computed,
        decimal employer,
        IReadOnlyDictionary<StatutoryKind, decimal> overrides)
    {
        var applied = overrides.TryGetValue(kind, out var amount) ? amount : computed;
        return new StatutoryLineResult(kind, computed, PayrollMoney.Rupees(applied), employer);
    }
}
