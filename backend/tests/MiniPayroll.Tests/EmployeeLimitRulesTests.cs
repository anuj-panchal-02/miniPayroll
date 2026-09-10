using MiniPayroll.Domain.Auth;
using MiniPayroll.Domain.Constants;

namespace MiniPayroll.Tests;

public class EmployeeLimitRulesTests
{
    [Fact]
    public void Accepts_the_configured_min_default_and_hard_cap()
    {
        Assert.True(EmployeeLimitRules.IsValid(PlatformLimits.MinEmployeeLimit));
        Assert.True(EmployeeLimitRules.IsValid(PlatformLimits.DefaultEmployeeLimit));
        Assert.True(EmployeeLimitRules.IsValid(PlatformLimits.HardEmployeeCap));
    }

    [Fact]
    public void Rejects_values_outside_the_configured_range()
    {
        Assert.False(EmployeeLimitRules.IsValid(PlatformLimits.MinEmployeeLimit - 1));
        Assert.False(EmployeeLimitRules.IsValid(PlatformLimits.HardEmployeeCap + 1));
    }

    [Fact]
    public void Default_limit_stays_within_the_hard_cap()
    {
        Assert.InRange(
            PlatformLimits.DefaultEmployeeLimit,
            PlatformLimits.MinEmployeeLimit,
            PlatformLimits.HardEmployeeCap);
    }

    [Fact]
    public void Invalid_message_uses_the_configured_range()
    {
        Assert.Equal(
            $"Employee limit must be between {PlatformLimits.MinEmployeeLimit} and {PlatformLimits.HardEmployeeCap}.",
            EmployeeLimitRules.InvalidMessage);
    }
}
