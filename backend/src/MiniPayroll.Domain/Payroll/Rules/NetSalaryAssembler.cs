using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Payroll;

internal static class NetSalaryAssembler
{
    public static PayrollEmployeeResult Assemble(
        PayrollEmployeeInput input,
        IReadOnlyList<PayrollResultLine> lines,
        int daysEmployed,
        decimal dailyRate,
        IReadOnlyList<string> warnings,
        decimal employerPf,
        decimal employerEsi)
    {
        var errors = new List<string>();
        var collectedWarnings = warnings.ToList();
        var period = input.Period;
        var attendance = input.Attendance;
        var gross = lines.Where(line => line.Type == SalaryComponentType.Earning).Sum(line => line.Amount);
        var totalDeductions = lines.Where(line => line.Type == SalaryComponentType.Deduction).Sum(line => line.Amount);
        var net = gross - totalDeductions;

        if (net < 0)
        {
            errors.Add(PayrollCalculationMessages.NegativeNet);
        }

        if (daysEmployed < period.CalendarDays)
        {
            collectedWarnings.Add(PayrollCalculationMessages.Prorated);
        }
        if (attendance.UnpaidLeave > PayrollCalculator.UnpaidLeaveWarningThreshold)
        {
            collectedWarnings.Add(PayrollCalculationMessages.HighUnpaidLeave);
        }
        if (net == 0)
        {
            collectedWarnings.Add(PayrollCalculationMessages.ZeroNet);
        }
        if (input.StructureEffectiveFrom is { } effective
            && effective > period.FirstDay
            && effective <= period.LastDay)
        {
            collectedWarnings.Add(PayrollCalculationMessages.StructureChangedMidMonth);
        }
        if (input.PreviousMonthNet is { } previous
            && previous > 0
            && Math.Abs(net - previous) > previous * PayrollCalculator.NetChangeWarningRatio)
        {
            collectedWarnings.Add(PayrollCalculationMessages.NetChangedFromPreviousMonth);
        }

        return new PayrollEmployeeResult(
            lines,
            gross,
            totalDeductions,
            net,
            daysEmployed,
            dailyRate,
            errors,
            collectedWarnings.Distinct().ToList(),
            employerPf,
            employerEsi);
    }
}
