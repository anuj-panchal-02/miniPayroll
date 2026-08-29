namespace MiniPayroll.Domain.Tenancy;

public interface ITenantContext
{
    Guid UserId { get; }
    Guid? CompanyId { get; }
    bool IsSuperadmin { get; }
}
