using MiniPayroll.Domain.Auth;

namespace MiniPayroll.Tests;

public class EmployeeLimitRulesTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(9, true)]
        [InlineData(20, true)]
        [InlineData(50, true)]
        [InlineData(51, false)]
    public void IsValid_matches_platform_cap(int limit, bool expected)
    {
        Assert.Equal(expected, EmployeeLimitRules.IsValid(limit));
    }
}
