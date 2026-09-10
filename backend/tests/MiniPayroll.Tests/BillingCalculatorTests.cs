using MiniPayroll.Domain.Billing;
using MiniPayroll.Domain.Payroll;

namespace MiniPayroll.Tests;

public class BillingCalculatorTests
{
    private static readonly PayrollPeriod August = new(2026, 8);

    [Theory]
    [InlineData("2026-08", 2026, 8)]
    [InlineData("2000-01", 2000, 1)]
    [InlineData("2100-12", 2100, 12)]
    public void Parses_yyyy_mm_periods(string value, int year, int month)
    {
        Assert.True(BillingCalculator.TryParsePeriod(value, out var period));
        Assert.Equal(year, period.Year);
        Assert.Equal(month, period.Month);
        Assert.Equal(value, BillingCalculator.FormatPeriod(period));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2026-8")]
    [InlineData("26-08")]
    [InlineData("2026-13")]
    [InlineData("2026-00")]
    [InlineData("1999-12")]
    public void Rejects_invalid_periods(string? value)
    {
        Assert.False(BillingCalculator.TryParsePeriod(value, out _));
    }

    [Fact]
    public void Finalized_run_wins_over_eligible_headcount()
    {
        var result = BillingCalculator.BillableEmployees(
            hasFinalizedRun: true,
            finalizedDistinctEmployees: 4,
            eligibleHeadcount: 1);

        Assert.Equal(4, result.Count);
        Assert.Equal(BillableSource.FinalizedPayroll, result.Source);
    }

    [Fact]
    public void Finalized_run_with_zero_employees_does_not_fall_back()
    {
        var result = BillingCalculator.BillableEmployees(
            hasFinalizedRun: true,
            finalizedDistinctEmployees: 0,
            eligibleHeadcount: 9);

        Assert.Equal(0, result.Count);
        Assert.Equal(BillableSource.FinalizedPayroll, result.Source);
    }

    [Fact]
    public void Missing_finalized_run_uses_eligible_headcount()
    {
        var result = BillingCalculator.BillableEmployees(
            hasFinalizedRun: false,
            finalizedDistinctEmployees: 0,
            eligibleHeadcount: 6);

        Assert.Equal(6, result.Count);
        Assert.Equal(BillableSource.ActiveHeadcount, result.Source);
    }

    [Fact]
    public void Full_month_amount_is_price_times_billable()
    {
        var activated = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal(294m, BillingCalculator.AmountDue(49m, 6, August, activated));
        Assert.False(BillingCalculator.IsProrated(August, activated));
    }

    [Fact]
    public void First_month_prorates_including_the_activation_day()
    {
        var activated = new DateTimeOffset(2026, 8, 10, 8, 0, 0, TimeSpan.Zero);

        // 22 remaining days in a 31-day August, inclusive of the 10th.
        Assert.Equal(208.65m, BillingCalculator.AmountDue(49m, 6, August, activated));
        Assert.True(BillingCalculator.IsProrated(August, activated));
    }

    [Fact]
    public void Activation_on_the_first_is_a_full_month()
    {
        var activated = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.Equal(147m, BillingCalculator.AmountDue(49m, 3, August, activated));
        Assert.False(BillingCalculator.IsProrated(August, activated));
    }

    [Fact]
    public void Periods_run_from_activation_month_through_now()
    {
        var activated = new DateTimeOffset(2026, 6, 20, 0, 0, 0, TimeSpan.Zero);
        var now = new DateTimeOffset(2026, 8, 15, 12, 0, 0, TimeSpan.Zero);

        var periods = BillingCalculator.PeriodsThrough(activated, now);

        Assert.Equal(
            new[] { new PayrollPeriod(2026, 6), new PayrollPeriod(2026, 7), new PayrollPeriod(2026, 8) },
            periods);
    }

    [Fact]
    public void Billing_end_stays_on_the_current_month_without_later_finalized_runs()
    {
        var now = new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);
        var end = BillingCalculator.BillingEnd(now, [August, new PayrollPeriod(2026, 9)]);
        Assert.Equal(new PayrollPeriod(2026, 9), end);
        Assert.Equal(
            new[] { August, new PayrollPeriod(2026, 9) },
            BillingCalculator.PeriodsThrough(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero), end));
    }

    [Fact]
    public void Billing_end_extends_through_the_latest_finalized_month()
    {
        var now = new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);
        var end = BillingCalculator.BillingEnd(
            now,
            [August, new PayrollPeriod(2026, 9), new PayrollPeriod(2026, 12)]);
        Assert.Equal(new PayrollPeriod(2026, 12), end);
        Assert.Equal(
            new[]
            {
                August,
                new PayrollPeriod(2026, 9),
                new PayrollPeriod(2026, 10),
                new PayrollPeriod(2026, 11),
                new PayrollPeriod(2026, 12)
            },
            BillingCalculator.PeriodsThrough(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero), end));
    }

    [Fact]
    public void Current_month_is_the_open_calendar_month()
    {
        var now = new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);
        Assert.True(BillingCalculator.IsCurrent(new PayrollPeriod(2026, 9), now));
        Assert.False(BillingCalculator.IsCurrent(August, now));
        Assert.False(BillingCalculator.IsCurrent(new PayrollPeriod(2026, 12), now));
    }

    [Theory]
    [InlineData("UPI")]
    [InlineData("NEFT")]
    [InlineData("Cash")]
    public void Accepts_offline_payment_modes(string mode)
    {
        Assert.True(BillingCalculator.IsAllowedMode(mode));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("upi")]
    [InlineData("Bank")]
    public void Rejects_unknown_payment_modes(string? mode)
    {
        Assert.False(BillingCalculator.IsAllowedMode(mode));
    }

    [Fact]
    public void Closed_months_are_those_before_the_current_utc_month()
    {
        var now = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        Assert.True(BillingCalculator.IsClosed(August, now));
        Assert.False(BillingCalculator.IsClosed(new PayrollPeriod(2026, 9), now));
        Assert.False(BillingCalculator.IsClosed(new PayrollPeriod(2026, 10), now));
    }

    [Fact]
    public void Due_date_is_the_last_moment_of_the_period()
    {
        Assert.Equal(
            new DateTimeOffset(2026, 8, 31, 23, 59, 59, TimeSpan.Zero),
            BillingCalculator.DueDate(August));
    }

    [Fact]
    public void Overdue_and_grace_follow_due_date()
    {
        var due = BillingCalculator.DueDate(August);
        var remaining = 98m;

        Assert.False(BillingCalculator.IsOverdue(remaining, due, due));
        Assert.True(BillingCalculator.IsOverdue(
            remaining, due, new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero)));
        Assert.False(BillingCalculator.IsOverdue(0m, due, new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero)));

        Assert.False(BillingCalculator.IsPastGrace(
            remaining, due, 7, new DateTimeOffset(2026, 9, 7, 23, 59, 59, TimeSpan.Zero)));
        Assert.True(BillingCalculator.IsPastGrace(
            remaining, due, 7, new DateTimeOffset(2026, 9, 8, 0, 0, 0, TimeSpan.Zero)));
        Assert.False(BillingCalculator.IsPastGrace(
            0m, due, 7, new DateTimeOffset(2026, 9, 20, 0, 0, 0, TimeSpan.Zero)));
    }
}
