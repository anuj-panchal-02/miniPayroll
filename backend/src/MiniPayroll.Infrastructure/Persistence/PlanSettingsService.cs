using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Billing;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Tenancy;

namespace MiniPayroll.Infrastructure.Persistence;

public enum PlanSettingsStatus
{
    Success,
    Forbidden,
    NotFound,
    InvalidInput
}

public sealed record PlatformPlan(Guid Id, string Name, decimal PricePerEmployee);

public sealed record PlanSettingsResult(PlanSettingsStatus Status, PlatformPlan? Plan = null);

public sealed class PlanSettingsService(MiniPayrollDbContext db, ITenantContext tenant)
{
    public async Task<PlanSettingsResult> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!tenant.IsSuperadmin)
        {
            return new PlanSettingsResult(PlanSettingsStatus.Forbidden);
        }

        var plan = await LoadBasicAsync(cancellationToken);
        return plan is null
            ? new PlanSettingsResult(PlanSettingsStatus.NotFound)
            : new PlanSettingsResult(PlanSettingsStatus.Success, ToPlan(plan));
    }

    public async Task<PlanSettingsResult> UpdateAsync(
        decimal pricePerEmployee,
        CancellationToken cancellationToken = default)
    {
        if (!tenant.IsSuperadmin)
        {
            return new PlanSettingsResult(PlanSettingsStatus.Forbidden);
        }

        if (!BillingCalculator.IsValidAmount(pricePerEmployee))
        {
            return new PlanSettingsResult(PlanSettingsStatus.InvalidInput);
        }

        var plan = await LoadBasicAsync(cancellationToken);
        if (plan is null)
        {
            return new PlanSettingsResult(PlanSettingsStatus.NotFound);
        }

        plan.PricePerEmployee = pricePerEmployee;
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            ActorUserId = tenant.UserId,
            Action = AuditActions.PlanPriceChange,
            Details = pricePerEmployee.ToString("0.##"),
            OccurredAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
        return new PlanSettingsResult(PlanSettingsStatus.Success, ToPlan(plan));
    }

    private Task<Plan?> LoadBasicAsync(CancellationToken cancellationToken) =>
        db.Plans.SingleOrDefaultAsync(
            plan => plan.Name == PlatformLimits.DefaultPlanName,
            cancellationToken);

    private static PlatformPlan ToPlan(Plan plan) =>
        new(plan.Id, plan.Name, plan.PricePerEmployee);
}
