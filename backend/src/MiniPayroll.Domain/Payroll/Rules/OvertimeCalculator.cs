using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Payroll;

internal static class OvertimeCalculator
{
    public static IReadOnlyList<PayrollResultLine> Calculate(IReadOnlyList<PayrollOvertimeEntry> overtime) =>
        overtime
            .Where(entry => entry.Hours > 0)
            .Select(entry => new PayrollResultLine(
                "Overtime",
                SalaryComponentType.Earning,
                PayrollLineKind.Overtime,
                PayrollMoney.Rupees(entry.Hours * entry.Rate!.Value)))
            .ToList();
}
