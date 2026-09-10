using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Payroll;

public readonly record struct PayrollPeriod(int Year, int Month)
{
    public int CalendarDays => DateTime.DaysInMonth(Year, Month);
    public DateOnly FirstDay => new(Year, Month, 1);
    public DateOnly LastDay => new(Year, Month, CalendarDays);

    public bool IsValid => Year is >= 2000 and <= 2100 && Month is >= 1 and <= 12;
}

public sealed record PayrollAttendance(
    decimal WorkingDays,
    decimal Present,
    decimal PaidLeave,
    decimal UnpaidLeave);

public sealed record PayrollStructureLine(
    string Name,
    SalaryComponentType Type,
    SalaryComponentValueType ValueType,
    decimal Value,
    int SortOrder);

public sealed record PayrollOvertimeEntry(decimal Hours, decimal? Rate);

public sealed record PayrollAmountEntry(string Name, decimal Amount);

public sealed record PayrollEmployeeInput(
    PayrollPeriod Period,
    DailyRateMethod DailyRateMethod,
    DateOnly? JoiningDate,
    DateOnly? ExitDate,
    PayrollAttendance Attendance,
    DateOnly? StructureEffectiveFrom,
    IReadOnlyList<PayrollStructureLine>? StructureLines,
    IReadOnlyList<PayrollOvertimeEntry>? Overtime = null,
    IReadOnlyList<PayrollAmountEntry>? Bonuses = null,
    IReadOnlyList<PayrollAmountEntry>? OneTimeDeductions = null,
    decimal? PreviousMonthNet = null);

public enum PayrollLineKind
{
    RecurringEarning = 0,
    Overtime = 1,
    Bonus = 2,
    RecurringDeduction = 3,
    UnpaidLeave = 4,
    OneTimeDeduction = 5
}

public sealed record PayrollResultLine(
    string Name,
    SalaryComponentType Type,
    PayrollLineKind Kind,
    decimal Amount);

public sealed record PayrollEmployeeResult(
    IReadOnlyList<PayrollResultLine> Lines,
    decimal GrossEarnings,
    decimal TotalDeductions,
    decimal NetSalary,
    int DaysEmployed,
    decimal DailyRate,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public bool HasBlockingErrors => Errors.Count > 0;
}

public static class PayrollCalculationMessages
{
    public const string AttendanceIdentity =
        "Attendance identity violated: Present + Paid Leave + Unpaid Leave must equal Working Days.";
    public const string MissingJoiningDate = "Employee has no joining date.";
    public const string NoEmploymentOverlap = "Employee was not employed during this payroll period.";
    public const string MissingStructure = "Employee has no salary structure configured.";
    public const string MissingBasicSalary = "Salary structure has no fixed Basic Salary earning.";
    public const string MissingOvertimeRate = "Overtime hours entered without an overtime rate.";
    public const string NegativeNet = "Net salary is negative.";

    public const string Prorated = "Salary prorated: the employee joined or left during this month.";
    public const string HighUnpaidLeave = "More than 5 unpaid leave days.";
    public const string ZeroNet = "Net salary is zero.";
    public const string StructureChangedMidMonth =
        "Salary structure changed during this month; the structure effective on the last day was used.";
    public const string NetChangedFromPreviousMonth =
        "Net salary differs from the previous month by more than 20%.";
}

public static class PayrollCalculator
{
    public const string BasicSalaryName = "Basic Salary";
    public const decimal UnpaidLeaveWarningThreshold = 5m;
    public const decimal NetChangeWarningRatio = 0.20m;

    public static PayrollEmployeeResult Calculate(PayrollEmployeeInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var errors = new List<string>();
        var warnings = new List<string>();
        var period = input.Period;
        var attendance = input.Attendance;

        if (attendance.Present + attendance.PaidLeave + attendance.UnpaidLeave != attendance.WorkingDays)
        {
            errors.Add(PayrollCalculationMessages.AttendanceIdentity);
        }

        var daysEmployed = 0;
        if (input.JoiningDate is not { } joining)
        {
            errors.Add(PayrollCalculationMessages.MissingJoiningDate);
        }
        else if (joining > period.LastDay || input.ExitDate < period.FirstDay)
        {
            errors.Add(PayrollCalculationMessages.NoEmploymentOverlap);
        }
        else
        {
            var employedFrom = joining > period.FirstDay ? joining : period.FirstDay;
            var employedTo = input.ExitDate is { } exit && exit < period.LastDay ? exit : period.LastDay;
            daysEmployed = employedTo.DayNumber - employedFrom.DayNumber + 1;
        }

        PayrollStructureLine? basic = null;
        if (input.StructureLines is not { Count: > 0 } structure)
        {
            errors.Add(PayrollCalculationMessages.MissingStructure);
            structure = [];
        }
        else
        {
            basic = structure.FirstOrDefault(line =>
                line.Name.Equals(BasicSalaryName, StringComparison.OrdinalIgnoreCase)
                && line.Type == SalaryComponentType.Earning
                && line.ValueType == SalaryComponentValueType.FixedAmount);
            if (basic is null)
            {
                errors.Add(PayrollCalculationMessages.MissingBasicSalary);
            }
        }

        var overtime = input.Overtime ?? [];
        if (overtime.Any(entry => entry.Hours > 0 && entry.Rate is not > 0m))
        {
            errors.Add(PayrollCalculationMessages.MissingOvertimeRate);
        }

        if (errors.Count > 0)
        {
            return new PayrollEmployeeResult([], 0m, 0m, 0m, 0, 0m, errors, warnings);
        }

        var calendarDays = period.CalendarDays;
        var basicValue = basic!.Value;

        // Resolve % of Basic to a 2 dp full-month value, matching salary-structure storage.
        decimal FullMonthValue(PayrollStructureLine line) =>
            line.ValueType == SalaryComponentValueType.FixedAmount
                ? line.Value
                : decimal.Round(basicValue * line.Value / 100m, 2, MidpointRounding.AwayFromZero);

        var orderedStructure = structure.OrderBy(line => line.SortOrder).ToList();
        var fullMonthEarnings = orderedStructure
            .Where(line => line.Type == SalaryComponentType.Earning)
            .Sum(FullMonthValue);

        var divisor = input.DailyRateMethod == DailyRateMethod.FixedThirty ? 30m : calendarDays;
        var dailyRate = fullMonthEarnings / divisor;

        var prorated = daysEmployed < calendarDays;
        var ratio = (decimal)daysEmployed / calendarDays;

        var lines = new List<PayrollResultLine>();
        foreach (var line in orderedStructure)
        {
            lines.Add(new PayrollResultLine(
                line.Name,
                line.Type,
                line.Type == SalaryComponentType.Earning
                    ? PayrollLineKind.RecurringEarning
                    : PayrollLineKind.RecurringDeduction,
                Rupees(FullMonthValue(line) * ratio)));
        }

        foreach (var entry in overtime.Where(entry => entry.Hours > 0))
        {
            lines.Add(new PayrollResultLine(
                "Overtime",
                SalaryComponentType.Earning,
                PayrollLineKind.Overtime,
                Rupees(entry.Hours * entry.Rate!.Value)));
        }

        foreach (var bonus in input.Bonuses ?? [])
        {
            lines.Add(new PayrollResultLine(
                bonus.Name,
                SalaryComponentType.Earning,
                PayrollLineKind.Bonus,
                Rupees(bonus.Amount)));
        }

        if (attendance.UnpaidLeave > 0)
        {
            lines.Add(new PayrollResultLine(
                "Unpaid Leave",
                SalaryComponentType.Deduction,
                PayrollLineKind.UnpaidLeave,
                Rupees(dailyRate * attendance.UnpaidLeave)));
        }

        foreach (var deduction in input.OneTimeDeductions ?? [])
        {
            lines.Add(new PayrollResultLine(
                deduction.Name,
                SalaryComponentType.Deduction,
                PayrollLineKind.OneTimeDeduction,
                Rupees(deduction.Amount)));
        }

        var gross = lines.Where(line => line.Type == SalaryComponentType.Earning).Sum(line => line.Amount);
        var totalDeductions = lines.Where(line => line.Type == SalaryComponentType.Deduction).Sum(line => line.Amount);
        var net = gross - totalDeductions;

        if (net < 0)
        {
            errors.Add(PayrollCalculationMessages.NegativeNet);
        }

        if (prorated)
        {
            warnings.Add(PayrollCalculationMessages.Prorated);
        }
        if (attendance.UnpaidLeave > UnpaidLeaveWarningThreshold)
        {
            warnings.Add(PayrollCalculationMessages.HighUnpaidLeave);
        }
        if (net == 0)
        {
            warnings.Add(PayrollCalculationMessages.ZeroNet);
        }
        if (input.StructureEffectiveFrom is { } effective
            && effective > period.FirstDay
            && effective <= period.LastDay)
        {
            warnings.Add(PayrollCalculationMessages.StructureChangedMidMonth);
        }
        if (input.PreviousMonthNet is { } previous
            && previous > 0
            && Math.Abs(net - previous) > previous * NetChangeWarningRatio)
        {
            warnings.Add(PayrollCalculationMessages.NetChangedFromPreviousMonth);
        }

        return new PayrollEmployeeResult(
            lines,
            gross,
            totalDeductions,
            net,
            daysEmployed,
            dailyRate,
            errors,
            warnings);
    }

    private static decimal Rupees(decimal amount) =>
        decimal.Round(amount, 0, MidpointRounding.AwayFromZero);
}
