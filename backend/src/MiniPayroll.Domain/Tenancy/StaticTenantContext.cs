namespace MiniPayroll.Domain.Tenancy;

public sealed class StaticTenantContext : ITenantContext
{
    public Guid UserId { get; init; }
    public Guid? CompanyId { get; init; }
    public bool IsSuperadmin { get; init; }
}
