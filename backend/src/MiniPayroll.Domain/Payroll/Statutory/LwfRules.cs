using MiniPayroll.Domain.Constants;

namespace MiniPayroll.Domain.Payroll.Statutory;

public readonly record struct LwfRule(string StateCode, decimal EmployeeAmount, int[] Months);

public static class LwfRules
{
    public static readonly IReadOnlyList<LwfRule> All =
    [
        new("MH", 25m, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]),
        new("KA", 20m, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]),
        new("KL", 50m, [6, 12])
    ];

    public static decimal AmountFor(string? companyState, int month)
    {
        var code = IndianStateCatalog.CodeFor(companyState);
        if (code is null)
        {
            return 0m;
        }

        var rule = All.FirstOrDefault(item => item.StateCode == code);
        if (rule.StateCode is null || !rule.Months.Contains(month))
        {
            return 0m;
        }

        return rule.EmployeeAmount;
    }
}
