using MiniPayroll.Domain.Auth;

namespace MiniPayroll.Tests;

public class SuperadminSeedRulesTests
{
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void MayBootstrap_requires_development_or_explicit_flag(
        bool isDevelopment, bool allowBootstrap, bool expected)
    {
        Assert.Equal(expected, SuperadminSeedRules.MayBootstrap(isDevelopment, allowBootstrap));
    }

    [Fact]
    public void RequirePassword_returns_configured_password()
    {
        Assert.Equal("ChangeMe_LocalOnly1!", SuperadminSeedRules.RequirePassword("ChangeMe_LocalOnly1!"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void RequirePassword_rejects_missing_password(string? password)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => SuperadminSeedRules.RequirePassword(password));
        Assert.Equal("Seed:SuperadminPassword is required to bootstrap Superadmin.", ex.Message);
    }
}
