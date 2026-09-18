using MiniPayroll.Domain.Constants;

namespace MiniPayroll.Domain.Payroll.Statutory;

/// <summary>
/// Configured LWF only: MH ₹25 every month; KA ₹20 every month; KL ₹50 in June and December.
/// Unlisted states and off months are ₹0. Do not add states here without an explicit product decision.
/// </summary>
public readonly record struct LwfRule(
    string StateCode,
    decimal EmployeeAmount,
    int[] Months,
    DateOnly? EffectiveFrom = null,
    DateOnly? EffectiveTo = null);

public static class LwfRules
{
    public static readonly IReadOnlyList<LwfRule> All =
    [
        new("MH", 25m, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]),
        new("KA", 20m, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]),
        new("KL", 50m, [6, 12])
    ];

    public static decimal AmountFor(string? companyState, int month, DateOnly? on = null)
    {
        var code = IndianStateCatalog.CodeFor(companyState);
        if (code is null)
        {
            return 0m;
        }

        var asOf = on ?? DateOnly.MaxValue;
        var rule = All.LastOrDefault(item =>
            item.StateCode == code
            && StatutoryRuleWindow.Covers(asOf, item.EffectiveFrom, item.EffectiveTo));
        if (rule.StateCode is null || !rule.Months.Contains(month))
        {
            return 0m;
        }

        return rule.EmployeeAmount;
    }
}
