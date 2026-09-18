using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Payroll;

namespace MiniPayroll.Tests;

/// <summary>
/// Unpaid-leave and working-day bounds reject rather than clamp. Identity
/// mismatches that stay inside those bounds can still be saved.
/// </summary>
public class UnpaidLeaveBoundsCharacterizationTests
{
    private static readonly DateOnly LongAgo = new(2025, 1, 1);

    private static readonly IReadOnlyList<PayrollStructureLine> BasicPlusHra =
    [
        new("Basic Salary", SalaryComponentType.Earning, SalaryComponentValueType.FixedAmount, 20000m, 0),
        new("HRA", SalaryComponentType.Earning, SalaryComponentValueType.PercentageOfBasic, 40m, 1),
    ];

    [Fact]
    public void Unpaid_leave_greater_than_working_days_blocks()
    {
        var result = Calculate(
            new PayrollPeriod(2026, 8),
            attendance: new PayrollAttendance(26, 0, 0, 27));

        Assert.Contains(PayrollCalculationMessages.AttendanceIdentity, result.Errors);
        Assert.Contains(PayrollCalculationMessages.UnpaidLeaveExceedsWorkingDays, result.Errors);
        Assert.Empty(result.Lines);
        Assert.Equal(0m, result.NetSalary);
    }

    [Fact]
    public void Negative_present_blocks_even_when_identity_holds()
    {
        var result = Calculate(
            new PayrollPeriod(2026, 8),
            attendance: new PayrollAttendance(26, -1, 0, 27));

        Assert.True(result.HasBlockingErrors);
        Assert.Contains(PayrollCalculationMessages.NegativeAttendance, result.Errors);
        Assert.Contains(PayrollCalculationMessages.UnpaidLeaveExceedsWorkingDays, result.Errors);
        Assert.Empty(result.Lines);
        Assert.Equal(0m, result.NetSalary);
    }

    [Fact]
    public void Unpaid_leave_greater_than_days_employed_blocks()
    {
        var result = Calculate(
            new PayrollPeriod(2026, 8),
            joining: new DateOnly(2026, 8, 28),
            attendance: new PayrollAttendance(26, 16, 0, 10));

        Assert.True(result.HasBlockingErrors);
        Assert.Contains(PayrollCalculationMessages.UnpaidLeaveExceedsDaysEmployed, result.Errors);
        Assert.DoesNotContain(PayrollCalculationMessages.NegativeNet, result.Errors);
        Assert.Empty(result.Lines);
        Assert.Equal(0m, result.NetSalary);
    }

    [Fact]
    public void Working_days_above_august_calendar_days_block()
    {
        var result = Calculate(
            new PayrollPeriod(2026, 8),
            attendance: new PayrollAttendance(32, 32, 0, 0));

        Assert.Contains(PayrollCalculationMessages.WorkingDaysExceedCalendar, result.Errors);
        Assert.Empty(result.Lines);
        Assert.Equal(0m, result.NetSalary);
    }

    [Fact]
    public void February_2026_rejects_30_working_days()
    {
        var result = Calculate(
            new PayrollPeriod(2026, 2),
            attendance: new PayrollAttendance(30, 30, 0, 0));

        Assert.Contains(PayrollCalculationMessages.WorkingDaysExceedCalendar, result.Errors);
        Assert.Empty(result.Lines);
        Assert.Equal(0m, result.NetSalary);
        Assert.True(PayrollInputRules.IsHalfDayQuantity(30m));
        Assert.False(PayrollInputRules.IsHalfDayQuantity(30m, 28m));
    }

    [Fact]
    public void Joiner_with_excessive_unpaid_leave_does_not_clamp()
    {
        var result = Calculate(
            new PayrollPeriod(2026, 8),
            joining: new DateOnly(2026, 8, 28),
            attendance: new PayrollAttendance(26, 16, 0, 10));

        Assert.Contains(PayrollCalculationMessages.UnpaidLeaveExceedsDaysEmployed, result.Errors);
        Assert.Empty(result.Lines);
        Assert.Equal(0m, result.DailyRate);
        Assert.Equal(0m, result.NetSalary);
    }

    [Fact]
    public void Exit_employee_with_excessive_unpaid_leave_blocks()
    {
        var result = Calculate(
            new PayrollPeriod(2026, 8),
            exit: new DateOnly(2026, 8, 5),
            attendance: new PayrollAttendance(26, 6, 0, 20));

        Assert.Contains(PayrollCalculationMessages.UnpaidLeaveExceedsDaysEmployed, result.Errors);
        Assert.DoesNotContain(PayrollCalculationMessages.NegativeNet, result.Errors);
        Assert.Empty(result.Lines);
        Assert.Equal(0m, result.NetSalary);
    }

    [Fact]
    public void Input_rules_cap_quantities_at_the_month_calendar()
    {
        Assert.False(PayrollInputRules.IsHalfDayQuantity(32m));
        Assert.True(PayrollInputRules.IsHalfDayQuantity(31m));
        Assert.True(PayrollInputRules.IsHalfDayQuantity(30m));
        Assert.False(PayrollInputRules.IsHalfDayQuantity(30m, 28m));
        Assert.True(PayrollInputRules.IsHalfDayQuantity(28m, 28m));
    }

    private static PayrollEmployeeResult Calculate(
        PayrollPeriod period,
        PayrollAttendance attendance,
        DateOnly? joining = null,
        DateOnly? exit = null) =>
        PayrollCalculator.Calculate(new PayrollEmployeeInput(
            period,
            DailyRateMethod.CalendarDays,
            joining ?? LongAgo,
            exit,
            attendance,
            LongAgo,
            BasicPlusHra));
}
