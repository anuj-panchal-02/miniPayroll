using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Payroll.Statutory;

namespace MiniPayroll.Domain.Payroll;

internal sealed record RecurringEarnings(
    IReadOnlyList<PayrollResultLine> Lines,
    IReadOnlyList<PayrollStructureLine> EarningStructure,
    decimal DailyRate,
    bool IgnoredStatutoryOnStructure);

internal static class RecurringEarningsCalculator
{
    public static RecurringEarnings Calculate(
        IReadOnlyList<PayrollStructureLine> structure,
        decimal basicValue,
        int daysEmployed,
        int calendarDays,
        DailyRateMethod method)
    {
        decimal FullMonthValue(PayrollStructureLine line) =>
            line.ValueType == SalaryComponentValueType.FixedAmount
                ? line.Value
                : PayrollMoney.TwoDecimals(basicValue * line.Value / 100m);

        var orderedStructure = structure.OrderBy(line => line.SortOrder).ToList();
        var ignoredStatutory = orderedStructure.Any(line =>
            line.Type == SalaryComponentType.Deduction
            || SalaryComponentKinds.IsStatutoryAmountName(line.Name));

        var earningLines = orderedStructure
            .Where(line => line.Type == SalaryComponentType.Earning)
            .ToList();
        var fullMonthEarnings = earningLines.Sum(FullMonthValue);
        var divisor = method == DailyRateMethod.FixedThirty ? 30m : calendarDays;
        var dailyRate = fullMonthEarnings / divisor;
        var ratio = Proration.Ratio(daysEmployed, calendarDays);

        var lines = earningLines
            .Select(line => new PayrollResultLine(
                line.Name,
                SalaryComponentType.Earning,
                PayrollLineKind.RecurringEarning,
                PayrollMoney.Rupees(FullMonthValue(line) * ratio)))
            .ToList();

        return new RecurringEarnings(lines, earningLines, dailyRate, ignoredStatutory);
    }
}
