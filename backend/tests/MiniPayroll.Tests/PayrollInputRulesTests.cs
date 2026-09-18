using MiniPayroll.Domain.Payroll;

namespace MiniPayroll.Tests;

public class PayrollInputRulesTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(0.5)]
    [InlineData(26)]
    [InlineData(31)]
    public void Half_day_quantities_accept_zero_to_thirty_one_in_half_steps(decimal value) =>
        Assert.True(PayrollInputRules.IsHalfDayQuantity(value));

    [Theory]
    [InlineData(-0.5)]
    [InlineData(0.25)]
    [InlineData(26.1)]
    [InlineData(31.5)]
    public void Half_day_quantities_reject_out_of_range_or_odd_steps(decimal value) =>
        Assert.False(PayrollInputRules.IsHalfDayQuantity(value));

    [Fact]
    public void Notes_cap_at_five_hundred_characters()
    {
        Assert.True(PayrollInputRules.IsValidNotes(null));
        Assert.True(PayrollInputRules.IsValidNotes(new string('x', 500)));
        Assert.False(PayrollInputRules.IsValidNotes(new string('x', 501)));
    }

    [Fact]
    public void Month_bounds_use_calendar_days_and_days_employed()
    {
        var august = new PayrollPeriod(2026, 8);
        var february = new PayrollPeriod(2026, 2);
        var longAgo = new DateOnly(2025, 1, 1);

        Assert.Equal(31, august.DaysEmployed(longAgo, null));
        Assert.Equal(4, august.DaysEmployed(new DateOnly(2026, 8, 28), null));
        Assert.Equal(5, august.DaysEmployed(longAgo, new DateOnly(2026, 8, 5)));
        Assert.Equal(28, february.DaysEmployed(longAgo, null));
        Assert.Null(august.DaysEmployed(null, null));

        Assert.False(PayrollInputRules.IsWithinMonthBounds(30, 30, 0, 0, february, longAgo, null));
        Assert.False(PayrollInputRules.IsWithinMonthBounds(32, 32, 0, 0, august, longAgo, null));
        Assert.False(PayrollInputRules.IsWithinMonthBounds(26, 0, 0, 27, august, longAgo, null));
        Assert.False(PayrollInputRules.IsWithinMonthBounds(26, 16, 0, 10, august, new DateOnly(2026, 8, 28), null));
        Assert.True(PayrollInputRules.IsWithinMonthBounds(26, 20, 0, 0, august, longAgo, null));
        Assert.False(PayrollInputRules.IsHalfDayQuantity(30m, 28m));
        Assert.True(PayrollInputRules.IsHalfDayQuantity(28m, 28m));
    }
}
