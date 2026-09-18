namespace MiniPayroll.Domain.Subscriptions;

public interface IEntitlementService
{
    Task<bool> CanUseFeature(Guid companyId, string featureCode, CancellationToken cancellationToken = default);

    Task<int> GetEmployeeLimit(Guid companyId, CancellationToken cancellationToken = default);

    Task<EmployeeUsage> GetActiveEmployeeUsage(Guid companyId, CancellationToken cancellationToken = default);

    Task<bool> CanAddEmployee(Guid companyId, CancellationToken cancellationToken = default);

    Task<bool> CanRunPayroll(Guid companyId, CancellationToken cancellationToken = default);

    Task<EntitlementSnapshot> GetSnapshot(Guid companyId, CancellationToken cancellationToken = default);
}
