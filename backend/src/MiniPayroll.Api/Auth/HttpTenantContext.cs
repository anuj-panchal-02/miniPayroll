using System.Security.Claims;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Tenancy;

namespace MiniPayroll.Api.Auth;

public sealed class HttpTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    public Guid UserId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : Guid.Empty;

    public Guid? CompanyId =>
        Guid.TryParse(accessor.HttpContext?.User.FindFirstValue("company_id"), out var companyId)
            ? companyId
            : null;

    public bool IsSuperadmin =>
        accessor.HttpContext?.User.IsInRole(RoleNames.Superadmin) == true;
}
