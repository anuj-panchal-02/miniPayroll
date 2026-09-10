using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Payroll;

namespace MiniPayroll.Tests;

public class PayrollCalculatorTests
{
    // August 2026 has 31 calendar days.
    private static readonly PayrollPeriod August = new(2026, 8);
    private static readonly DateOnly LongAgo = new(2025, 1, 1);

    private static readonly IReadOnlyList<PayrollStructureLine> BasicPlusHra =
    [
        new("Basic Salary", SalaryComponentType.Earning, SalaryComponentValueType.FixedAmount, 20000m, 0),
        new("HRA", SalaryComponentType.Earning, SalaryComponentValueType.PercentageOfBasic, 40m, 1),
    ];

    private static PayrollEmployeeInput Input(
        PayrollAttendance? attendance = null,
        DateOnly? joining = null,
        DateOnly? exit = null,
        IReadOnlyList<PayrollStructureLine>? structure = null,
        DateOnly? structureEffectiveFrom = null,
        DailyRateMethod method = DailyRateMethod.CalendarDays,
        IReadOnlyList<PayrollOvertimeEntry>? overtime = null,
        IReadOnlyList<PayrollAmountEntry>? bonuses = null,
        IReadOnlyList<PayrollAmountEntry>? deductions = null,
        decimal? previousNet = null) => new(
        August,
        method,
        joining ?? LongAgo,
        exit,
        attendance ?? new PayrollAttendance(26, 26, 0, 0),
        structureEffectiveFrom ?? LongAgo,
        structure ?? BasicPlusHra,
        overtime,
        bonuses,
        deductions,
        previousNet);

    [Fact]
    public void Full_month_pays_all_recurring_earnings_with_resolved_percentages()
    {
        var result = PayrollCalculator.Calculate(Input());

        Assert.False(result.HasBlockingErrors);
        Assert.Empty(result.Warnings);
        Assert.Equal(31, result.DaysEmployed);
        Assert.Equal(28000m / 31m, result.DailyRate);
        Assert.Equal(28000m, result.GrossEarnings);
        Assert.Equal(0m, result.TotalDeductions);
        Assert.Equal(28000m, result.NetSalary);
        Assert.Collection(
            result.Lines,
            line => Assert.Equal(("Basic Salary", 20000m), (line.Name, line.Amount)),
            line => Assert.Equal(("HRA", 8000m), (line.Name, line.Amount)));
    }

    [Fact]
    public void Unpaid_leave_deducts_daily_rate_per_day_including_half_days()
    {
        var result = PayrollCalculator.Calculate(Input(
            attendance: new PayrollAttendance(26, 25.5m, 0, 0.5m)));

        // 28000 / 31 × 0.5 = 451.61… → 452
        var unpaid = Assert.Single(result.Lines, line => line.Kind == PayrollLineKind.UnpaidLeave);
        Assert.Equal(452m, unpaid.Amount);
        Assert.Equal(28000m - 452m, result.NetSalary);
    }

    [Fact]
    public void Fixed_thirty_method_divides_by_thirty_regardless_of_month_length()
    {
        var result = PayrollCalculator.Calculate(Input(
            method: DailyRateMethod.FixedThirty,
            attendance: new PayrollAttendance(26, 25, 0, 1)));

        Assert.Equal(28000m / 30m, result.DailyRate);
        var unpaid = Assert.Single(result.Lines, line => line.Kind == PayrollLineKind.UnpaidLeave);
        Assert.Equal(933m, unpaid.Amount); // 933.33 → 933
    }

    [Fact]
    public void Joining_mid_month_prorates_each_recurring_line_by_calendar_days()
    {
        var result = PayrollCalculator.Calculate(Input(joining: new DateOnly(2026, 8, 16)));

        Assert.Equal(16, result.DaysEmployed);
        // Basic 20000 × 16/31 = 10322.58 → 10323; HRA 8000 × 16/31 = 4129.03 → 4129
        Assert.Collection(
            result.Lines,
            line => Assert.Equal(10323m, line.Amount),
            line => Assert.Equal(4129m, line.Amount));
        Assert.Contains(PayrollCalculationMessages.Prorated, result.Warnings);
        // Daily rate still uses the unprorated monthly salary.
        Assert.Equal(28000m / 31m, result.DailyRate);
    }

    [Fact]
    public void Leaving_mid_month_prorates_from_month_start_to_exit_inclusive()
    {
        var result = PayrollCalculator.Calculate(Input(exit: new DateOnly(2026, 8, 10)));

        Assert.Equal(10, result.DaysEmployed);
        Assert.Collection(
            result.Lines,
            line => Assert.Equal(6452m, line.Amount),   // 20000 × 10/31
            line => Assert.Equal(2581m, line.Amount));  // 8000 × 10/31
        Assert.Contains(PayrollCalculationMessages.Prorated, result.Warnings);
    }

    [Fact]
    public void Recurring_deductions_prorate_but_one_time_items_do_not()
    {
        var structure = new List<PayrollStructureLine>(BasicPlusHra)
        {
            new("Provident Fund (PF)", SalaryComponentType.Deduction, SalaryComponentValueType.FixedAmount, 1800m, 2),
        };
        var result = PayrollCalculator.Calculate(Input(
            joining: new DateOnly(2026, 8, 16),
            structure: structure,
            deductions: [new PayrollAmountEntry("Salary advance recovery", 1000m)]));

        var pf = Assert.Single(result.Lines, line => line.Kind == PayrollLineKind.RecurringDeduction);
        Assert.Equal(929m, pf.Amount); // 1800 × 16/31 = 929.03 → 929
        var advance = Assert.Single(result.Lines, line => line.Kind == PayrollLineKind.OneTimeDeduction);
        Assert.Equal(1000m, advance.Amount);
    }

    [Fact]
    public void Overtime_and_bonus_add_to_earnings_without_touching_daily_rate()
    {
        var result = PayrollCalculator.Calculate(Input(
            overtime: [new PayrollOvertimeEntry(10m, 150m)],
            bonuses: [new PayrollAmountEntry("Festival Bonus", 2000m)]));

        Assert.Equal(28000m / 31m, result.DailyRate);
        Assert.Equal(28000m + 1500m + 2000m, result.GrossEarnings);
        Assert.Equal(31500m, result.NetSalary);
    }

    [Fact]
    public void Overtime_hours_without_a_rate_is_a_blocking_error()
    {
        var result = PayrollCalculator.Calculate(Input(
            overtime: [new PayrollOvertimeEntry(10m, null)]));

        Assert.Contains(PayrollCalculationMessages.MissingOvertimeRate, result.Errors);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void Lines_round_to_the_nearest_rupee_half_up()
    {
        var structure = new[]
        {
            new PayrollStructureLine(
                "Basic Salary", SalaryComponentType.Earning, SalaryComponentValueType.FixedAmount, 1000.50m, 0),
        };
        var result = PayrollCalculator.Calculate(Input(structure: structure));

        Assert.Equal(1001m, result.Lines.Single().Amount);
        Assert.Equal(1001m, result.NetSalary);
    }

    [Fact]
    public void Attendance_identity_violation_blocks_calculation()
    {
        var result = PayrollCalculator.Calculate(Input(
            attendance: new PayrollAttendance(26, 24, 1, 0)));

        Assert.Contains(PayrollCalculationMessages.AttendanceIdentity, result.Errors);
        Assert.Empty(result.Lines);
    }

    [Fact]
    public void Employment_outside_the_period_blocks_calculation()
    {
        var joinsNextMonth = PayrollCalculator.Calculate(Input(joining: new DateOnly(2026, 9, 1)));
        var leftLastMonth = PayrollCalculator.Calculate(Input(exit: new DateOnly(2026, 7, 31)));
        var noJoiningDate = PayrollCalculator.Calculate(Input() with { JoiningDate = null });

        Assert.Contains(PayrollCalculationMessages.NoEmploymentOverlap, joinsNextMonth.Errors);
        Assert.Contains(PayrollCalculationMessages.NoEmploymentOverlap, leftLastMonth.Errors);
        Assert.Contains(PayrollCalculationMessages.MissingJoiningDate, noJoiningDate.Errors);
    }

    [Fact]
    public void Missing_structure_or_basic_salary_blocks_calculation()
    {
        var noStructure = PayrollCalculator.Calculate(Input() with { StructureLines = null });
        var noBasic = PayrollCalculator.Calculate(Input(structure:
        [
            new("HRA", SalaryComponentType.Earning, SalaryComponentValueType.FixedAmount, 8000m, 0),
        ]));

        Assert.Contains(PayrollCalculationMessages.MissingStructure, noStructure.Errors);
        Assert.Contains(PayrollCalculationMessages.MissingBasicSalary, noBasic.Errors);
    }

    [Fact]
    public void Negative_net_is_a_blocking_error_and_zero_net_is_a_warning()
    {
        var negative = PayrollCalculator.Calculate(Input(
            deductions: [new PayrollAmountEntry("Loan installment", 30000m)]));
        var zero = PayrollCalculator.Calculate(Input(
            deductions: [new PayrollAmountEntry("Loan installment", 28000m)]));

        Assert.Contains(PayrollCalculationMessages.NegativeNet, negative.Errors);
        Assert.False(zero.HasBlockingErrors);
        Assert.Contains(PayrollCalculationMessages.ZeroNet, zero.Warnings);
    }

    [Fact]
    public void Structure_taking_effect_mid_month_warns_but_uses_the_last_day_structure()
    {
        var result = PayrollCalculator.Calculate(Input(
            structureEffectiveFrom: new DateOnly(2026, 8, 15)));

        Assert.False(result.HasBlockingErrors);
        Assert.Equal(28000m, result.NetSalary);
        Assert.Contains(PayrollCalculationMessages.StructureChangedMidMonth, result.Warnings);

        var firstOfMonth = PayrollCalculator.Calculate(Input(
            structureEffectiveFrom: new DateOnly(2026, 8, 1)));
        Assert.DoesNotContain(
            PayrollCalculationMessages.StructureChangedMidMonth, firstOfMonth.Warnings);
    }

    [Fact]
    public void Unpaid_leave_above_five_days_and_large_net_change_warn()
    {
        var result = PayrollCalculator.Calculate(Input(
            attendance: new PayrollAttendance(26, 20, 0, 6),
            previousNet: 28000m));

        Assert.Contains(PayrollCalculationMessages.HighUnpaidLeave, result.Warnings);
        // 6 unpaid days ≈ ₹5,419 deduction → net 22,581; 19.4% drop, no net-change warning.
        Assert.DoesNotContain(PayrollCalculationMessages.NetChangedFromPreviousMonth, result.Warnings);

        var bigChange = PayrollCalculator.Calculate(Input(previousNet: 20000m));
        Assert.Contains(PayrollCalculationMessages.NetChangedFromPreviousMonth, bigChange.Warnings);
    }

    [Fact]
    public void Same_input_produces_identical_results()
    {
        var input = Input(
            joining: new DateOnly(2026, 8, 5),
            attendance: new PayrollAttendance(24, 22.5m, 1, 0.5m),
            overtime: [new PayrollOvertimeEntry(7.5m, 175m)],
            bonuses: [new PayrollAmountEntry("Incentive", 1234m)],
            deductions: [new PayrollAmountEntry("TDS", 500m)]);

        var first = PayrollCalculator.Calculate(input);
        var second = PayrollCalculator.Calculate(input);

        Assert.Equal(first.NetSalary, second.NetSalary);
        Assert.Equal(first.GrossEarnings, second.GrossEarnings);
        Assert.Equal(first.TotalDeductions, second.TotalDeductions);
        Assert.Equal(
            first.Lines.Select(line => (line.Name, line.Type, line.Kind, line.Amount)),
            second.Lines.Select(line => (line.Name, line.Type, line.Kind, line.Amount)));
    }
}
