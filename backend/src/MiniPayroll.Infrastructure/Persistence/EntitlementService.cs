using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Subscriptions;
using MiniPayroll.Domain.Tenancy;

namespace MiniPayroll.Infrastructure.Persistence;

public sealed class EntitlementService(
    MiniPayrollDbContext db,
    ITenantContext tenant,
    TimeProvider? time = null) : IEntitlementService
{
    public async Task<bool> CanUseFeature(
        Guid companyId,
        string featureCode,
        CancellationToken cancellationToken = default)
    {
        var loaded = await LoadAsync(companyId, cancellationToken);
        return loaded is not null
            && EntitlementRules.CanUseFeature(loaded.Status, loaded.Features, featureCode);
    }

    public async Task<int> GetEmployeeLimit(
        Guid companyId,
        CancellationToken cancellationToken = default) =>
        (await GetSnapshot(companyId, cancellationToken)).Usage.MaximumAllowed;

    public async Task<EmployeeUsage> GetActiveEmployeeUsage(
        Guid companyId,
        CancellationToken cancellationToken = default) =>
        (await GetSnapshot(companyId, cancellationToken)).Usage;

    public async Task<bool> CanAddEmployee(
        Guid companyId,
        CancellationToken cancellationToken = default) =>
        (await GetSnapshot(companyId, cancellationToken)).Usage.CanAdd;

    public async Task<bool> CanRunPayroll(
        Guid companyId,
        CancellationToken cancellationToken = default) =>
        (await GetSnapshot(companyId, cancellationToken)).CanRunPayroll;

    public async Task<EntitlementSnapshot> GetSnapshot(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var loaded = await LoadAsync(companyId, cancellationToken);
        if (loaded is null)
        {
            return EntitlementSnapshot.None;
        }

        return EntitlementRules.Evaluate(
            loaded.Status,
            loaded.Features,
            loaded.ActiveCount,
            loaded.EmployeeLimit,
            loaded.MaxActiveEmployees,
            loaded.TrialEndsAt,
            (time ?? TimeProvider.System).GetUtcNow());
    }

    private async Task<LoadedEntitlement?> LoadAsync(Guid companyId, CancellationToken cancellationToken)
    {
        if (!tenant.IsSuperadmin && tenant.CompanyId != companyId)
        {
            return null;
        }

        var subscription = await db.Subscriptions
            .AsNoTracking()
            .Include(item => item.Plan)
            .ThenInclude(plan => plan.Features)
            .SingleOrDefaultAsync(item => item.CompanyId == companyId, cancellationToken);
        if (subscription is null)
        {
            return null;
        }

        var activeCount = await db.Employees.CountAsync(
            employee => employee.CompanyId == companyId && employee.Status == EmployeeStatus.Active,
            cancellationToken);

        return new LoadedEntitlement(
            subscription.Status,
            subscription.Plan.Features.ToList(),
            activeCount,
            subscription.EmployeeLimit,
            subscription.Plan.MaxActiveEmployees,
            subscription.TrialEndsAt);
    }

    private sealed record LoadedEntitlement(
        SubscriptionStatus Status,
        IReadOnlyList<PlanFeature> Features,
        int ActiveCount,
        int EmployeeLimit,
        int MaxActiveEmployees,
        DateTimeOffset? TrialEndsAt);
}
