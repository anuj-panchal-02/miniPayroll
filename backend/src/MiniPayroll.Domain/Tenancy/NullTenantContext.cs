namespace MiniPayroll.Domain.Tenancy;

public sealed class NullTenantContext : ITenantContext
{
    public static readonly NullTenantContext Instance = new();

    public Guid UserId => Guid.Empty;
    public Guid? CompanyId => null;
    public bool IsSuperadmin => true;
}
