using MiniPayroll.Domain.Auth;

namespace MiniPayroll.Tests;

public class TemporaryPasswordRulesTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData("Tmp_TestAdmin1!", true)]
    public void IsProvided_requires_non_whitespace(string? password, bool expected)
    {
        Assert.Equal(expected, TemporaryPasswordRules.IsProvided(password));
    }
}
