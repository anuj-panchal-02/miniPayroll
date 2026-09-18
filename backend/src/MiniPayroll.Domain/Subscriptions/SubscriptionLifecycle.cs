using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Subscriptions;

public enum SubscriptionLifecycleStatus
{
    Success,
    DuplicateLiveSubscription,
    InvalidTransition,
    PlanInactive
}

public sealed record SubscriptionLifecycleResult(
    SubscriptionLifecycleStatus Status,
    Subscription? Subscription = null,
    IReadOnlyList<SubscriptionEvent>? Events = null);

public static class SubscriptionLifecycle
{
    public static SubscriptionLifecycleResult Create(
        Guid companyId,
        Plan plan,
        int employeeLimit,
        BillingCycle billingCycle,
        DateTimeOffset now,
        IReadOnlyCollection<Subscription> existingForCompany,
        Guid? actorUserId = null)
    {
        if (!plan.IsActive)
        {
            return new SubscriptionLifecycleResult(SubscriptionLifecycleStatus.PlanInactive);
        }

        if (existingForCompany.Any(item => SubscriptionStatuses.IsLive(item.Status)))
        {
            return new SubscriptionLifecycleResult(SubscriptionLifecycleStatus.DuplicateLiveSubscription);
        }

        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Trialing,
            BillingCycle = billingCycle,
            EmployeeLimit = employeeLimit,
            GracePeriodDays = PlatformLimits.DefaultGracePeriodDays,
            CreatedAt = now,
            UpdatedAt = now
        };

        var events = new List<SubscriptionEvent>();
        if (plan.TrialDays > 0)
        {
            subscription.TrialStartedAt = now;
            subscription.TrialEndsAt = now.AddDays(plan.TrialDays);
            events.Add(Event(subscription, SubscriptionEventType.TrialStarted, now, actorUserId));
        }

        return new SubscriptionLifecycleResult(
            SubscriptionLifecycleStatus.Success,
            subscription,
            events);
    }

    public static SubscriptionLifecycleResult AssignPlan(
        Subscription subscription,
        Plan plan,
        BillingCycle? billingCycle,
        DateTimeOffset now,
        Guid? actorUserId = null,
        int? employeeLimit = null)
    {
        if (!plan.IsActive)
        {
            return new SubscriptionLifecycleResult(SubscriptionLifecycleStatus.PlanInactive);
        }

        subscription.PlanId = plan.Id;
        subscription.Plan = plan;
        if (billingCycle is { } cycle)
        {
            subscription.BillingCycle = cycle;
        }

        if (employeeLimit is { } limit)
        {
            subscription.EmployeeLimit = limit;
        }

        subscription.UpdatedAt = now;
        var changed = Event(subscription, SubscriptionEventType.PlanChanged, now, actorUserId);
        return new SubscriptionLifecycleResult(
            SubscriptionLifecycleStatus.Success,
            subscription,
            [changed]);
    }

    public static SubscriptionLifecycleResult Execute(
        Subscription subscription,
        SubscriptionCommand command,
        DateTimeOffset now,
        Guid? actorUserId = null)
    {
        if (command == SubscriptionCommand.RequestCancel)
        {
            return RequestCancel(subscription, now, actorUserId);
        }

        if (command == SubscriptionCommand.Activate)
        {
            return Activate(subscription, now, actorUserId);
        }

        if (command == SubscriptionCommand.Reactivate)
        {
            return Reactivate(subscription, now, actorUserId);
        }

        var target = command switch
        {
            SubscriptionCommand.MarkPastDue => SubscriptionStatus.PastDue,
            SubscriptionCommand.EnterGrace => SubscriptionStatus.GracePeriod,
            SubscriptionCommand.Suspend => SubscriptionStatus.Suspended,
            SubscriptionCommand.CancelNow => SubscriptionStatus.Cancelled,
            SubscriptionCommand.Expire => SubscriptionStatus.Expired,
            _ => subscription.Status
        };

        if (!SubscriptionStatusTransitions.CanExecute(subscription.Status, command))
        {
            return new SubscriptionLifecycleResult(SubscriptionLifecycleStatus.InvalidTransition);
        }

        if (subscription.Status == target)
        {
            return new SubscriptionLifecycleResult(SubscriptionLifecycleStatus.Success, subscription, []);
        }

        subscription.Status = target;
        subscription.UpdatedAt = now;
        if (command == SubscriptionCommand.CancelNow)
        {
            subscription.CancelledAt ??= now;
            subscription.CancelAtPeriodEnd = false;
        }

        var events = new List<SubscriptionEvent>();
        if (SubscriptionStatusTransitions.EventFor(command) is { } type)
        {
            events.Add(Event(subscription, type, now, actorUserId));
        }

        return new SubscriptionLifecycleResult(SubscriptionLifecycleStatus.Success, subscription, events);
    }

    public static SubscriptionLifecycleResult Transition(
        Subscription subscription,
        SubscriptionStatus to,
        DateTimeOffset now,
        Guid? actorUserId = null)
    {
        if (subscription.Status == to)
        {
            return new SubscriptionLifecycleResult(SubscriptionLifecycleStatus.Success, subscription, []);
        }

        var command = to switch
        {
            SubscriptionStatus.Active => subscription.Status is SubscriptionStatus.Suspended
                or SubscriptionStatus.Cancelled
                or SubscriptionStatus.Expired
                ? SubscriptionCommand.Reactivate
                : SubscriptionCommand.Activate,
            SubscriptionStatus.PastDue => SubscriptionCommand.MarkPastDue,
            SubscriptionStatus.GracePeriod => SubscriptionCommand.EnterGrace,
            SubscriptionStatus.Suspended => SubscriptionCommand.Suspend,
            SubscriptionStatus.Cancelled => SubscriptionCommand.CancelNow,
            SubscriptionStatus.Expired => SubscriptionCommand.Expire,
            _ => (SubscriptionCommand?)null
        };

        return command is { } named
            ? Execute(subscription, named, now, actorUserId)
            : new SubscriptionLifecycleResult(SubscriptionLifecycleStatus.InvalidTransition);
    }

    public static SubscriptionLifecycleResult Activate(
        Subscription subscription,
        DateTimeOffset now,
        Guid? actorUserId = null)
    {
        if (subscription.Status == SubscriptionStatus.Active
            && subscription.CurrentPeriodStart is not null)
        {
            return new SubscriptionLifecycleResult(SubscriptionLifecycleStatus.Success, subscription, []);
        }

        if (!SubscriptionStatusTransitions.CanExecute(subscription.Status, SubscriptionCommand.Activate)
            && subscription.Status != SubscriptionStatus.Active)
        {
            return new SubscriptionLifecycleResult(SubscriptionLifecycleStatus.InvalidTransition);
        }

        var from = subscription.Status;
        subscription.Status = SubscriptionStatus.Active;
        ApplyPeriod(subscription, now, reset: false);
        subscription.UpdatedAt = now;

        var events = new List<SubscriptionEvent>();
        if (from != SubscriptionStatus.Active)
        {
            events.Add(Event(subscription, SubscriptionEventType.Activated, now, actorUserId));
        }

        return new SubscriptionLifecycleResult(SubscriptionLifecycleStatus.Success, subscription, events);
    }

    public static SubscriptionLifecycleResult Reactivate(
        Subscription subscription,
        DateTimeOffset now,
        Guid? actorUserId = null)
    {
        if (!SubscriptionStatusTransitions.CanExecute(subscription.Status, SubscriptionCommand.Reactivate))
        {
            return new SubscriptionLifecycleResult(SubscriptionLifecycleStatus.InvalidTransition);
        }

        subscription.Status = SubscriptionStatus.Active;
        subscription.CancelledAt = null;
        subscription.CancelAtPeriodEnd = false;
        ApplyPeriod(subscription, now, reset: true);
        subscription.UpdatedAt = now;
        return new SubscriptionLifecycleResult(
            SubscriptionLifecycleStatus.Success,
            subscription,
            [Event(subscription, SubscriptionEventType.Reactivated, now, actorUserId)]);
    }

    public static SubscriptionEvent Record(
        Subscription subscription,
        SubscriptionEventType type,
        DateTimeOffset now,
        Guid? actorUserId = null)
    {
        var added = Event(subscription, type, now, actorUserId);
        subscription.Events.Add(added);
        subscription.UpdatedAt = now;
        return added;
    }

    private static SubscriptionLifecycleResult RequestCancel(
        Subscription subscription,
        DateTimeOffset now,
        Guid? actorUserId)
    {
        if (!SubscriptionStatusTransitions.CanExecute(subscription.Status, SubscriptionCommand.RequestCancel))
        {
            return new SubscriptionLifecycleResult(SubscriptionLifecycleStatus.InvalidTransition);
        }

        if (subscription.CancelAtPeriodEnd)
        {
            return new SubscriptionLifecycleResult(SubscriptionLifecycleStatus.Success, subscription, []);
        }

        subscription.CancelAtPeriodEnd = true;
        subscription.UpdatedAt = now;
        return new SubscriptionLifecycleResult(
            SubscriptionLifecycleStatus.Success,
            subscription,
            [Event(subscription, SubscriptionEventType.CancellationRequested, now, actorUserId)]);
    }

    private static void ApplyPeriod(Subscription subscription, DateTimeOffset now, bool reset)
    {
        if (reset || subscription.CurrentPeriodStart is null)
        {
            subscription.CurrentPeriodStart = now;
        }

        if (reset || subscription.CurrentPeriodEnd is null)
        {
            subscription.CurrentPeriodEnd = PeriodEnd(now);
        }

        if (reset || subscription.NextBillingDate is null)
        {
            subscription.NextBillingDate = subscription.DueDate ?? subscription.CurrentPeriodEnd;
        }

        if (reset || subscription.DueDate is null)
        {
            subscription.DueDate = subscription.NextBillingDate;
        }
    }

    private static DateTimeOffset PeriodEnd(DateTimeOffset now) =>
        new(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month), 23, 59, 59, now.Offset);

    private static SubscriptionEvent Event(
        Subscription subscription,
        SubscriptionEventType type,
        DateTimeOffset now,
        Guid? actorUserId) =>
        new()
        {
            Id = Guid.NewGuid(),
            SubscriptionId = subscription.Id,
            CompanyId = subscription.CompanyId,
            Type = type,
            OccurredAt = now,
            ActorUserId = actorUserId
        };
}
