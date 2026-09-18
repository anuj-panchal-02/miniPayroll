using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Billing;
using MiniPayroll.Domain.Billing.Payments;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Subscriptions;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Payments;

namespace MiniPayroll.Infrastructure.Persistence;

public enum SubscriptionCommandStatus
{
    Success,
    Forbidden,
    CompanyNotFound,
    InvalidTransition,
    AdminRequired,
    PlanNotFound,
    PlanInactive,
    InvalidInput,
    UsageExceedsPlanLimit,
    PaymentFailed,
    ProviderUnavailable
}

public sealed record SubscriptionCommandResult(
    SubscriptionCommandStatus Status,
    Subscription? Subscription = null,
    string? Error = null);

public sealed class SubscriptionLifecycleService(
    MiniPayrollDbContext db,
    ITenantContext tenant,
    TimeProvider? time = null,
    PaymentGatewayService? gateway = null)
{
    public Task<SubscriptionCommandResult> ActivateAsync(
        Guid companyId,
        Guid actorUserId,
        bool requireAdmin,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(
            companyId,
            SubscriptionCommand.Activate,
            actorUserId,
            requireAdmin,
            cancellationToken);

    public Task<SubscriptionCommandResult> MarkPastDueAsync(
        Guid companyId,
        Guid actorUserId,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(companyId, SubscriptionCommand.MarkPastDue, actorUserId, false, cancellationToken);

    public Task<SubscriptionCommandResult> EnterGraceAsync(
        Guid companyId,
        Guid actorUserId,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(companyId, SubscriptionCommand.EnterGrace, actorUserId, false, cancellationToken);

    public Task<SubscriptionCommandResult> SuspendAsync(
        Guid companyId,
        Guid actorUserId,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(companyId, SubscriptionCommand.Suspend, actorUserId, false, cancellationToken);

    public Task<SubscriptionCommandResult> RequestCancelAsync(
        Guid companyId,
        Guid actorUserId,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(companyId, SubscriptionCommand.RequestCancel, actorUserId, false, cancellationToken);

    public async Task<SubscriptionCommandResult> CancelNowAsync(
        Guid companyId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        var result = await ExecuteAsync(
            companyId,
            SubscriptionCommand.CancelNow,
            actorUserId,
            false,
            cancellationToken);
        if (result.Status != SubscriptionCommandStatus.Success)
        {
            return result;
        }

        var providerError = await TryCancelProviderRecurringAsync(companyId, cancellationToken);
        return providerError is null ? result : result with { Error = providerError };
    }

    public Task<SubscriptionCommandResult> ExpireAsync(
        Guid companyId,
        Guid actorUserId,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(companyId, SubscriptionCommand.Expire, actorUserId, false, cancellationToken);

    public Task<SubscriptionCommandResult> ReactivateAsync(
        Guid companyId,
        Guid actorUserId,
        CancellationToken cancellationToken = default) =>
        ExecuteAsync(companyId, SubscriptionCommand.Reactivate, actorUserId, false, cancellationToken);

    public async Task<SubscriptionCommandResult> ChangePlanAsync(
        Guid companyId,
        string planCode,
        BillingCycle? billingCycle,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (!tenant.IsSuperadmin)
        {
            return new SubscriptionCommandResult(SubscriptionCommandStatus.Forbidden);
        }

        if (string.IsNullOrWhiteSpace(planCode))
        {
            return new SubscriptionCommandResult(
                SubscriptionCommandStatus.InvalidInput,
                Error: "Plan code is required.");
        }

        var companies = db.Companies
            .Include(item => item.Subscription)
            .ThenInclude(item => item!.Plan)
            .ThenInclude(item => item.Prices)
            .AsQueryable();
        if (tenant.IsSuperadmin)
        {
            companies = companies.IgnoreQueryFilters();
        }

        var company = await companies.FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        if (company?.Subscription is null)
        {
            return new SubscriptionCommandResult(SubscriptionCommandStatus.CompanyNotFound);
        }

        var code = planCode.Trim();
        var plans = tenant.IsSuperadmin ? db.Plans.IgnoreQueryFilters() : db.Plans;
        var target = await plans
            .Include(item => item.Prices)
            .FirstOrDefaultAsync(item => item.Code == code, cancellationToken);

        var usage = await db.Employees
            .IgnoreQueryFilters()
            .CountAsync(
                employee => employee.CompanyId == companyId && employee.Status == EmployeeStatus.Active,
                cancellationToken);

        var now = (time ?? TimeProvider.System).GetUtcNow();
        var evaluation = PlanChangeRules.Evaluate(company.Subscription, target, usage, now, billingCycle);
        if (evaluation == PlanChangeStatus.SamePlan)
        {
            return new SubscriptionCommandResult(SubscriptionCommandStatus.Success, company.Subscription);
        }

        if (evaluation != PlanChangeStatus.Success || target is null)
        {
            return evaluation switch
            {
                PlanChangeStatus.PlanNotFound => new SubscriptionCommandResult(
                    SubscriptionCommandStatus.PlanNotFound,
                    Error: "The plan was not found."),
                PlanChangeStatus.PlanInactive => new SubscriptionCommandResult(
                    SubscriptionCommandStatus.PlanInactive,
                    Error: "The plan is not active."),
                PlanChangeStatus.UsageExceedsPlanLimit => new SubscriptionCommandResult(
                    SubscriptionCommandStatus.UsageExceedsPlanLimit,
                    Error: PlanChangeRules.UsageExceededMessage),
                _ => new SubscriptionCommandResult(SubscriptionCommandStatus.InvalidTransition)
            };
        }

        var previousPlanId = company.Subscription.PlanId;
        var previousPlan = company.Subscription.Plan;
        var previousCycle = company.Subscription.BillingCycle;
        var previousLimit = company.Subscription.EmployeeLimit;
        var previousUpdatedAt = company.Subscription.UpdatedAt;

        var assigned = SubscriptionLifecycle.AssignPlan(
            company.Subscription,
            target,
            billingCycle,
            now,
            actorUserId,
            PlanChangeRules.EmployeeLimitFor(target));
        if (assigned.Status != SubscriptionLifecycleStatus.Success)
        {
            return new SubscriptionCommandResult(
                assigned.Status == SubscriptionLifecycleStatus.PlanInactive
                    ? SubscriptionCommandStatus.PlanInactive
                    : SubscriptionCommandStatus.InvalidTransition);
        }

        var invoice = await DraftInvoiceIfNeededAsync(
            company,
            target,
            usage,
            now,
            cancellationToken);

        var provider = await SyncProviderRecurringAsync(
            company,
            target,
            usage,
            now,
            cancellationToken);
        if (provider is not null)
        {
            company.Subscription.PlanId = previousPlanId;
            company.Subscription.Plan = previousPlan;
            company.Subscription.BillingCycle = previousCycle;
            company.Subscription.EmployeeLimit = previousLimit;
            company.Subscription.UpdatedAt = previousUpdatedAt;
            return provider;
        }

        if (assigned.Events is { Count: > 0 })
        {
            db.SubscriptionEvents.AddRange(assigned.Events);
        }

        if (invoice is not null)
        {
            db.Invoices.Add(invoice);
        }

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            ActorUserId = actorUserId,
            Action = AuditActions.SubscriptionPlanChange,
            OccurredAt = now
        });

        await db.SaveChangesAsync(cancellationToken);
        return new SubscriptionCommandResult(SubscriptionCommandStatus.Success, company.Subscription);
    }

    public Task<SubscriptionCommandResult> ActivateFromTrustedPaymentAsync(
        Guid companyId,
        Guid? actorUserId,
        CancellationToken cancellationToken = default) =>
        ActivateFromTrustedPaymentAsync(companyId, actorUserId, persist: true, cancellationToken);

    public async Task<SubscriptionCommandResult> ActivateFromTrustedPaymentAsync(
        Guid companyId,
        Guid? actorUserId,
        bool persist,
        CancellationToken cancellationToken = default)
    {
        var company = await LoadTrustedCompanyAsync(companyId, cancellationToken);
        if (company?.Subscription is null)
        {
            return new SubscriptionCommandResult(SubscriptionCommandStatus.CompanyNotFound);
        }

        var alreadyActive = company.Subscription.Status == SubscriptionStatus.Active;
        var now = (time ?? TimeProvider.System).GetUtcNow();
        var command = TrustedPaymentCommand(company.Subscription.Status);
        if (command is { } named && !alreadyActive)
        {
            ApplyTrusted(company, named, now, actorUserId);
        }

        if (!alreadyActive)
        {
            db.SubscriptionEvents.Add(new SubscriptionEvent
            {
                Id = Guid.NewGuid(),
                SubscriptionId = company.Subscription.Id,
                CompanyId = company.Id,
                Type = SubscriptionEventType.PaymentSucceeded,
                OccurredAt = now,
                ActorUserId = actorUserId
            });
            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                ActorUserId = actorUserId ?? Guid.Empty,
                Action = AuditActions.PaymentVerified,
                OccurredAt = now
            });
        }

        if (persist)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return new SubscriptionCommandResult(SubscriptionCommandStatus.Success, company.Subscription);
    }

    public async Task<SubscriptionCommandResult> ApplyTrustedCommandAsync(
        Guid companyId,
        SubscriptionCommand command,
        Guid? actorUserId,
        bool persist,
        CancellationToken cancellationToken = default)
    {
        var company = await LoadTrustedCompanyAsync(companyId, cancellationToken);
        if (company?.Subscription is null)
        {
            return new SubscriptionCommandResult(SubscriptionCommandStatus.CompanyNotFound);
        }

        if (AlreadySatisfied(company.Subscription.Status, command))
        {
            return new SubscriptionCommandResult(SubscriptionCommandStatus.Success, company.Subscription);
        }

        var applied = ApplyTrusted(company, command, (time ?? TimeProvider.System).GetUtcNow(), actorUserId);
        if (applied.Status != SubscriptionCommandStatus.Success)
        {
            return applied;
        }

        if (persist)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return applied;
    }

    private Task<Company?> LoadTrustedCompanyAsync(Guid companyId, CancellationToken cancellationToken) =>
        db.Companies
            .IgnoreQueryFilters()
            .Include(item => item.Subscription)
            .ThenInclude(item => item!.Plan)
            .FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken);

    private SubscriptionCommandResult ApplyTrusted(
        Company company,
        SubscriptionCommand command,
        DateTimeOffset now,
        Guid? actorUserId)
    {
        var beforeStatus = company.Subscription!.Status;
        var beforeCancelAtPeriodEnd = company.Subscription.CancelAtPeriodEnd;
        var applied = SubscriptionLifecycle.Execute(company.Subscription, command, now, actorUserId);
        if (applied.Status != SubscriptionLifecycleStatus.Success)
        {
            return new SubscriptionCommandResult(SubscriptionCommandStatus.InvalidTransition);
        }

        var firstActivation = false;
        if (command is SubscriptionCommand.Activate or SubscriptionCommand.Reactivate
            && company.ActivatedAt is null)
        {
            company.ActivatedAt = now;
            firstActivation = true;
        }

        if (applied.Events is { Count: > 0 })
        {
            db.SubscriptionEvents.AddRange(applied.Events);
        }

        var changed = firstActivation
            || applied.Events is { Count: > 0 }
            || company.Subscription.Status != beforeStatus
            || company.Subscription.CancelAtPeriodEnd != beforeCancelAtPeriodEnd;
        if (changed)
        {
            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                ActorUserId = actorUserId ?? Guid.Empty,
                Action = AuditActionFor(command),
                OccurredAt = now
            });
        }

        return new SubscriptionCommandResult(SubscriptionCommandStatus.Success, company.Subscription);
    }

    private static bool AlreadySatisfied(SubscriptionStatus status, SubscriptionCommand command) =>
        command switch
        {
            SubscriptionCommand.Activate or SubscriptionCommand.Reactivate =>
                status == SubscriptionStatus.Active,
            SubscriptionCommand.Suspend => status == SubscriptionStatus.Suspended,
            SubscriptionCommand.CancelNow => status == SubscriptionStatus.Cancelled,
            _ => false
        };

    private static SubscriptionCommand? TrustedPaymentCommand(SubscriptionStatus status)
    {
        if (SubscriptionStatusTransitions.CanExecute(status, SubscriptionCommand.Reactivate))
        {
            return SubscriptionCommand.Reactivate;
        }

        if (status == SubscriptionStatus.Active
            || SubscriptionStatusTransitions.CanExecute(status, SubscriptionCommand.Activate))
        {
            return SubscriptionCommand.Activate;
        }

        return null;
    }

    public async Task<SubscriptionCommandResult> ExecuteAsync(
        Guid companyId,
        SubscriptionCommand command,
        Guid actorUserId,
        bool requireAdmin,
        CancellationToken cancellationToken = default)
    {
        if (!tenant.IsSuperadmin)
        {
            return new SubscriptionCommandResult(SubscriptionCommandStatus.Forbidden);
        }

        var companies = db.Companies
            .Include(item => item.Subscription)
            .ThenInclude(item => item!.Plan)
            .AsQueryable();
        if (tenant.IsSuperadmin)
        {
            companies = companies.IgnoreQueryFilters();
        }

        var company = await companies.FirstOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        if (company?.Subscription is null)
        {
            return new SubscriptionCommandResult(SubscriptionCommandStatus.CompanyNotFound);
        }

        if (requireAdmin && command == SubscriptionCommand.Activate)
        {
            var admin = await CompanyAdminLookup.GetAsync(db, companyId, cancellationToken);
            if (!admin.HasAdmin)
            {
                return new SubscriptionCommandResult(SubscriptionCommandStatus.AdminRequired);
            }
        }

        var now = (time ?? TimeProvider.System).GetUtcNow();
        var beforeStatus = company.Subscription.Status;
        var beforeCancelAtPeriodEnd = company.Subscription.CancelAtPeriodEnd;
        var applied = SubscriptionLifecycle.Execute(company.Subscription, command, now, actorUserId);
        if (applied.Status != SubscriptionLifecycleStatus.Success)
        {
            return new SubscriptionCommandResult(SubscriptionCommandStatus.InvalidTransition);
        }

        var firstActivation = false;
        if (command is SubscriptionCommand.Activate or SubscriptionCommand.Reactivate
            && company.ActivatedAt is null)
        {
            company.ActivatedAt = now;
            firstActivation = true;
        }

        if (applied.Events is { Count: > 0 })
        {
            db.SubscriptionEvents.AddRange(applied.Events);
        }

        var changed = firstActivation
            || applied.Events is { Count: > 0 }
            || company.Subscription.Status != beforeStatus
            || company.Subscription.CancelAtPeriodEnd != beforeCancelAtPeriodEnd;
        if (changed)
        {
            db.AuditLogs.Add(new AuditLog
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                ActorUserId = actorUserId,
                Action = AuditActionFor(command),
                OccurredAt = now
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        return new SubscriptionCommandResult(SubscriptionCommandStatus.Success, company.Subscription);
    }

    private static string AuditActionFor(SubscriptionCommand command) =>
        command switch
        {
            SubscriptionCommand.Activate => AuditActions.CompanyActivate,
            SubscriptionCommand.MarkPastDue => AuditActions.SubscriptionPastDue,
            SubscriptionCommand.EnterGrace => AuditActions.SubscriptionGrace,
            SubscriptionCommand.Suspend => AuditActions.SubscriptionSuspend,
            SubscriptionCommand.RequestCancel or SubscriptionCommand.CancelNow =>
                AuditActions.SubscriptionCancel,
            SubscriptionCommand.Expire => AuditActions.SubscriptionExpire,
            SubscriptionCommand.Reactivate => AuditActions.SubscriptionReactivate,
            _ => AuditActions.CompanyActivate
        };

    private async Task<Invoice?> DraftInvoiceIfNeededAsync(
        Company company,
        Plan plan,
        int usage,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var periodStart = company.Subscription!.CurrentPeriodStart ?? MonthStart(now);
        var periodEnd = company.Subscription.CurrentPeriodEnd ?? MonthEnd(now);
        var invoices = tenant.IsSuperadmin ? db.Invoices.IgnoreQueryFilters() : db.Invoices;
        var hasInvoice = await invoices.AnyAsync(
            invoice => invoice.CompanyId == company.Id
                && invoice.PeriodStart == periodStart
                && invoice.PeriodEnd == periodEnd
                && invoice.Status != InvoiceStatus.Void,
            cancellationToken);
        if (hasInvoice)
        {
            return null;
        }

        var cycle = company.Subscription.BillingCycle;
        var price = PlanPricing.Current(plan.Prices, cycle, now);
        var created = InvoiceLifecycle.Create(
            company.Id,
            company.Subscription.Id,
            periodStart,
            periodEnd,
            Math.Max(usage, 1),
            price?.Amount ?? plan.PricePerEmployee,
            price?.Currency ?? PlatformLimits.CurrencyCode,
            plan.Name,
            now);
        return created.Status == InvoiceLifecycleStatus.Success ? created.Invoice : null;
    }

    private async Task<SubscriptionCommandResult?> SyncProviderRecurringAsync(
        Company company,
        Plan plan,
        int usage,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (gateway is null)
        {
            return null;
        }

        var providerSubscriptionId = await LatestProviderSubscriptionIdAsync(company.Id, cancellationToken);
        if (providerSubscriptionId is null)
        {
            return null;
        }

        var cancelled = await gateway.CancelRecurringAsync(
            new PaymentCancelRecurringRequest(
                providerSubscriptionId,
                $"plan-change-cancel:{company.Id}:{now.UtcTicks}"),
            cancellationToken);
        if (cancelled.Status is PaymentProviderStatus.Timeout or PaymentProviderStatus.Unavailable)
        {
            return new SubscriptionCommandResult(
                SubscriptionCommandStatus.ProviderUnavailable,
                Error: cancelled.Error);
        }

        var cycle = company.Subscription!.BillingCycle;
        var periodStart = company.Subscription.CurrentPeriodStart ?? MonthStart(now);
        var periodEnd = company.Subscription.CurrentPeriodEnd ?? MonthEnd(now);
        var unitPrice = PlanPricing.AmountOrFallback(plan.Prices, cycle, now, plan.PricePerEmployee);
        var currency = PlanPricing.Current(plan.Prices, cycle, now)?.Currency ?? PlatformLimits.CurrencyCode;
        var amount = InvoiceTotals.LineAmount(Math.Max(usage, 1), unitPrice);
        var created = await gateway.CreateRecurringAsync(
            new PaymentRecurringRequest(
                company.Id,
                plan.Code,
                currency,
                amount,
                $"plan-change:{company.Id}:{plan.Code}:{now.UtcTicks}",
                periodStart,
                periodEnd),
            cancellationToken);

        if (created.Status == PaymentProviderStatus.Failed)
        {
            return new SubscriptionCommandResult(
                SubscriptionCommandStatus.PaymentFailed,
                Error: created.Error);
        }

        if (created.Status is PaymentProviderStatus.Timeout or PaymentProviderStatus.Unavailable)
        {
            return new SubscriptionCommandResult(
                SubscriptionCommandStatus.ProviderUnavailable,
                Error: created.Error);
        }

        if (created.ProviderSubscriptionId is { } nextId)
        {
            var intent = await LatestProviderIntentAsync(company.Id, cancellationToken);
            if (intent is not null)
            {
                intent.ProviderSubscriptionId = nextId;
                intent.Amount = amount;
                intent.UpdatedAt = now;
            }
        }

        return null;
    }

    private async Task<string?> TryCancelProviderRecurringAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        if (gateway is null)
        {
            return null;
        }

        var providerSubscriptionId = await LatestProviderSubscriptionIdAsync(companyId, cancellationToken);
        if (providerSubscriptionId is null)
        {
            return null;
        }

        var now = (time ?? TimeProvider.System).GetUtcNow();
        var cancelled = await gateway.CancelRecurringAsync(
            new PaymentCancelRecurringRequest(
                providerSubscriptionId,
                $"cancel-now:{companyId}:{now.UtcTicks}"),
            cancellationToken);
        return cancelled.Status is PaymentProviderStatus.Failed
            or PaymentProviderStatus.Timeout
            or PaymentProviderStatus.Unavailable
            ? cancelled.Error
            : null;
    }

    private Task<string?> LatestProviderSubscriptionIdAsync(
        Guid companyId,
        CancellationToken cancellationToken) =>
        db.PaymentIntents
            .IgnoreQueryFilters()
            .Where(intent =>
                intent.CompanyId == companyId
                && intent.ProviderSubscriptionId != null
                && intent.ProviderSubscriptionId != "")
            .OrderByDescending(intent => intent.UpdatedAt)
            .Select(intent => intent.ProviderSubscriptionId)
            .FirstOrDefaultAsync(cancellationToken);

    private Task<PaymentIntent?> LatestProviderIntentAsync(
        Guid companyId,
        CancellationToken cancellationToken) =>
        db.PaymentIntents
            .IgnoreQueryFilters()
            .Where(intent =>
                intent.CompanyId == companyId
                && intent.ProviderSubscriptionId != null
                && intent.ProviderSubscriptionId != "")
            .OrderByDescending(intent => intent.UpdatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    private static DateTimeOffset MonthStart(DateTimeOffset now) =>
        new(now.Year, now.Month, 1, 0, 0, 0, now.Offset);

    private static DateTimeOffset MonthEnd(DateTimeOffset now) =>
        new(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month), 23, 59, 59, now.Offset);
}
