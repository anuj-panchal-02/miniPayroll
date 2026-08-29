using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Infrastructure.Identity;

namespace MiniPayroll.Api.Auth;

public sealed class JwtTokenService(IConfiguration configuration)
{
    public string CreateToken(ApplicationUser user, IList<string> roles)
    {
        var jwt = configuration.GetSection("Jwt");
        var key = jwt["Key"] ?? throw new InvalidOperationException("Jwt:Key is not configured.");
        var issuer = jwt["Issuer"] ?? "miniPayroll";
        var audience = jwt["Audience"] ?? "miniPayroll";
        var expiryMinutes = int.TryParse(jwt["ExpiryMinutes"], out var minutes) ? minutes : 480;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("must_change_password", user.MustChangePassword ? "true" : "false")
        };

        if (user.CompanyId is Guid companyId)
        {
            claims.Add(new Claim("company_id", companyId.ToString()));
        }

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
            claims.Add(new Claim("role", role));
        }

        if (roles.Contains(RoleNames.Superadmin))
        {
            claims.Add(new Claim("is_superadmin", "true"));
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var token = new JwtSecurityToken(
            issuer,
            audience,
            claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
