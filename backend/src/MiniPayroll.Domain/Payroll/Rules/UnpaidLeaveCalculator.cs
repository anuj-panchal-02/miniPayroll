using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Payroll;

internal sealed record UnpaidLeaveDeduction(decimal Amount, IReadOnlyList<PayrollResultLine> Lines);

internal static class UnpaidLeaveCalculator
{
    public static UnpaidLeaveDeduction Calculate(decimal unpaidLeaveDays, decimal dailyRate)
    {
        if (unpaidLeaveDays <= 0)
        {
            return new UnpaidLeaveDeduction(0m, []);
        }

        var amount = PayrollMoney.Rupees(dailyRate * unpaidLeaveDays);
        return new UnpaidLeaveDeduction(
            amount,
            [
                new PayrollResultLine(
                    "Unpaid Leave",
                    SalaryComponentType.Deduction,
                    PayrollLineKind.UnpaidLeave,
                    amount)
            ]);
    }
}
