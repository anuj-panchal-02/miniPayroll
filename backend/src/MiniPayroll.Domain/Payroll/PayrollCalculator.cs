using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Payroll.Statutory;

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
    int SortOrder,
    SalaryComponentKind Kind = SalaryComponentKind.OtherEarning);

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
    decimal? PreviousMonthNet = null,
    StatutoryPolicy? Statutory = null,
    IReadOnlyList<StatutoryOverride>? Overrides = null);

public enum PayrollLineKind
{
    RecurringEarning = 0,
    Overtime = 1,
    Bonus = 2,
    RecurringDeduction = 3,
    UnpaidLeave = 4,
    OneTimeDeduction = 5,
    Statutory = 6
}

public sealed record PayrollResultLine(
    string Name,
    SalaryComponentType Type,
    PayrollLineKind Kind,
    decimal Amount,
    StatutoryKind? StatutoryKind = null,
    decimal? ComputedAmount = null);

public sealed record PayrollEmployeeResult(
    IReadOnlyList<PayrollResultLine> Lines,
    decimal GrossEarnings,
    decimal TotalDeductions,
    decimal NetSalary,
    int DaysEmployed,
    decimal DailyRate,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    decimal EmployerPf = 0m,
    decimal EmployerEsi = 0m)
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
    public const string IgnoredStatutoryStructure =
        "Statutory amounts on the salary structure were ignored; PF, ESI, PT, and LWF are calculated at payroll.";
    public const string StatutoryOverrideApplied = "A statutory amount was overridden for this run.";
    public const string EsiWagesAboveThreshold =
        "ESI wages are above the ₹21,000 eligibility band; contribution continues because the employee is covered.";
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
                ResolveKind(line) == SalaryComponentKind.Basic
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

        decimal FullMonthValue(PayrollStructureLine line) =>
            line.ValueType == SalaryComponentValueType.FixedAmount
                ? line.Value
                : decimal.Round(basicValue * line.Value / 100m, 2, MidpointRounding.AwayFromZero);

        var orderedStructure = structure.OrderBy(line => line.SortOrder).ToList();
        if (orderedStructure.Any(line =>
            line.Type == SalaryComponentType.Deduction
            || SalaryComponentKinds.IsStatutoryAmountName(line.Name)))
        {
            warnings.Add(PayrollCalculationMessages.IgnoredStatutoryStructure);
        }

        var earningLines = orderedStructure
            .Where(line => line.Type == SalaryComponentType.Earning)
            .ToList();
        var fullMonthEarnings = earningLines.Sum(FullMonthValue);

        var divisor = input.DailyRateMethod == DailyRateMethod.FixedThirty ? 30m : calendarDays;
        var dailyRate = fullMonthEarnings / divisor;

        var prorated = daysEmployed < calendarDays;
        var ratio = (decimal)daysEmployed / calendarDays;

        var lines = new List<PayrollResultLine>();
        foreach (var line in earningLines)
        {
            lines.Add(new PayrollResultLine(
                line.Name,
                SalaryComponentType.Earning,
                PayrollLineKind.RecurringEarning,
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

        var unpaidLeaveAmount = 0m;
        if (attendance.UnpaidLeave > 0)
        {
            unpaidLeaveAmount = Rupees(dailyRate * attendance.UnpaidLeave);
            lines.Add(new PayrollResultLine(
                "Unpaid Leave",
                SalaryComponentType.Deduction,
                PayrollLineKind.UnpaidLeave,
                unpaidLeaveAmount));
        }

        foreach (var deduction in input.OneTimeDeductions ?? [])
        {
            lines.Add(new PayrollResultLine(
                deduction.Name,
                SalaryComponentType.Deduction,
                PayrollLineKind.OneTimeDeduction,
                Rupees(deduction.Amount)));
        }

        var employerPf = 0m;
        var employerEsi = 0m;
        if (input.Statutory is { } policy)
        {
            var (pfWages, esiWages) = WageBases(earningLines, lines, unpaidLeaveAmount);
            var overtimeAmount = lines.Where(line => line.Kind == PayrollLineKind.Overtime)
                .Sum(line => line.Amount);
            esiWages += overtimeAmount;
            var ptWages = lines.Where(line => line.Type == SalaryComponentType.Earning)
                .Sum(line => line.Amount) - unpaidLeaveAmount;
            if (ptWages < 0)
            {
                ptWages = 0;
            }

            var statutory = StatutoryCalculator.Calculate(
                policy,
                period.LastDay,
                period.Month,
                pfWages,
                esiWages,
                ptWages,
                input.Overrides);

            foreach (var line in statutory.Where(item => item.Applied != 0 || item.Computed != 0))
            {
                lines.Add(new PayrollResultLine(
                    StatutoryLabels.Name(line.Kind),
                    SalaryComponentType.Deduction,
                    PayrollLineKind.Statutory,
                    line.Applied,
                    line.Kind,
                    line.Computed));
                if (line.Applied != line.Computed)
                {
                    warnings.Add(PayrollCalculationMessages.StatutoryOverrideApplied);
                }
            }

            employerPf = statutory.Single(item => item.Kind == StatutoryKind.PfEmployee).EmployerAmount;
            employerEsi = statutory.Single(item => item.Kind == StatutoryKind.EsiEmployee).EmployerAmount;
            var esiRule = EsiRules.For(period.LastDay);
            if (policy.EsiApplicable && policy.EsiCovered && esiWages > esiRule.EligibilityCeiling)
            {
                warnings.Add(PayrollCalculationMessages.EsiWagesAboveThreshold);
            }
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
            warnings.Distinct().ToList(),
            employerPf,
            employerEsi);
    }

    private static (decimal PfWages, decimal EsiWages) WageBases(
        IReadOnlyList<PayrollStructureLine> earningLines,
        IReadOnlyList<PayrollResultLine> lines,
        decimal unpaidLeaveAmount)
    {
        var recurring = lines.Where(line => line.Kind == PayrollLineKind.RecurringEarning).ToList();
        var totalRecurring = recurring.Sum(line => line.Amount);
        decimal Reduce(PayrollResultLine line)
        {
            if (totalRecurring <= 0 || unpaidLeaveAmount <= 0)
            {
                return line.Amount;
            }

            return Math.Max(0m, line.Amount - unpaidLeaveAmount * line.Amount / totalRecurring);
        }

        var pfWages = 0m;
        var esiWages = 0m;
        foreach (var line in recurring)
        {
            var reduced = Reduce(line);
            esiWages += reduced;
            var structure = earningLines.FirstOrDefault(item => item.Name == line.Name);
            var kind = structure is null ? SalaryComponentKinds.FromName(line.Name) : ResolveKind(structure);
            if (kind is SalaryComponentKind.Basic or SalaryComponentKind.Da)
            {
                pfWages += reduced;
            }
        }

        return (decimal.Round(pfWages, 2, MidpointRounding.AwayFromZero),
            decimal.Round(esiWages, 2, MidpointRounding.AwayFromZero));
    }

    private static SalaryComponentKind ResolveKind(PayrollStructureLine line) =>
        line.Kind == SalaryComponentKind.OtherEarning
            ? SalaryComponentKinds.FromName(line.Name)
            : line.Kind;

    private static decimal Rupees(decimal amount) =>
        decimal.Round(amount, 0, MidpointRounding.AwayFromZero);
}
