using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Payroll;

internal static class OneTimeDeductionCalculator
{
    public static IReadOnlyList<PayrollResultLine> Calculate(IReadOnlyList<PayrollAmountEntry>? deductions) =>
        (deductions ?? [])
            .Select(deduction => new PayrollResultLine(
                deduction.Name,
                SalaryComponentType.Deduction,
                PayrollLineKind.OneTimeDeduction,
                PayrollMoney.Rupees(deduction.Amount)))
            .ToList();
}
