using Microsoft.Extensions.Configuration;
using MiniPayroll.Api.Auth;
using MiniPayroll.Infrastructure.Identity;

namespace MiniPayroll.Tests;

public class JwtTokenServiceTests
{
    [Fact]
    public void CreateToken_throws_when_signing_key_is_missing()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "miniPayroll",
                ["Jwt:Audience"] = "miniPayroll"
            })
            .Build();
        var service = new JwtTokenService(configuration);
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "a@b.example" };

        var ex = Assert.Throws<InvalidOperationException>(() => service.CreateToken(user, []));
        Assert.Equal("Jwt:Key is not configured.", ex.Message);
    }

    [Fact]
    public void CreateToken_throws_when_signing_key_is_too_short()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "short-dev-key",
                ["Jwt:Issuer"] = "miniPayroll",
                ["Jwt:Audience"] = "miniPayroll"
            })
            .Build();
        var service = new JwtTokenService(configuration);
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = "a@b.example" };

        var ex = Assert.Throws<InvalidOperationException>(() => service.CreateToken(user, []));
        Assert.Equal("Jwt:Key must be at least 32 UTF-8 bytes.", ex.Message);
    }
}
