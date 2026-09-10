using MiniPayroll.Domain.Auth;

namespace MiniPayroll.Tests;

public class JwtSigningKeyRulesTests
{
    [Fact]
    public void Require_returns_key_when_it_is_at_least_32_utf8_bytes()
    {
        const string key = "12345678901234567890123456789012";
        Assert.Equal(key, JwtSigningKeyRules.Require(key));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Require_rejects_missing_key(string? key)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => JwtSigningKeyRules.Require(key));
        Assert.Equal("Jwt:Key is not configured.", ex.Message);
    }

    [Fact]
    public void Require_rejects_key_shorter_than_32_utf8_bytes()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => JwtSigningKeyRules.Require("short-dev-key"));
        Assert.Equal("Jwt:Key must be at least 32 UTF-8 bytes.", ex.Message);
    }
}
