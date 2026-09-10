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
}
