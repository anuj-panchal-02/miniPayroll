using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Payroll;

internal static class BonusCalculator
{
    public static IReadOnlyList<PayrollResultLine> Calculate(IReadOnlyList<PayrollAmountEntry>? bonuses) =>
        (bonuses ?? [])
            .Select(bonus => new PayrollResultLine(
                bonus.Name,
                SalaryComponentType.Earning,
                PayrollLineKind.Bonus,
                PayrollMoney.Rupees(bonus.Amount)))
            .ToList();
}
