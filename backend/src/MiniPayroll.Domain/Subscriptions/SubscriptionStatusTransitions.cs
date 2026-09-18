using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Subscriptions;

public static class SubscriptionStatusTransitions
{
    public static bool CanTransition(SubscriptionStatus from, SubscriptionStatus to)
    {
        if (from == to)
        {
            return true;
        }

        return (from, to) switch
        {
            (SubscriptionStatus.Trialing, SubscriptionStatus.Active) => true,
            (SubscriptionStatus.Trialing, SubscriptionStatus.Cancelled) => true,
            (SubscriptionStatus.Trialing, SubscriptionStatus.Expired) => true,
            (SubscriptionStatus.Active, SubscriptionStatus.PastDue) => true,
            (SubscriptionStatus.Active, SubscriptionStatus.GracePeriod) => true,
            (SubscriptionStatus.Active, SubscriptionStatus.Suspended) => true,
            (SubscriptionStatus.Active, SubscriptionStatus.Cancelled) => true,
            (SubscriptionStatus.PastDue, SubscriptionStatus.Active) => true,
            (SubscriptionStatus.PastDue, SubscriptionStatus.GracePeriod) => true,
            (SubscriptionStatus.PastDue, SubscriptionStatus.Suspended) => true,
            (SubscriptionStatus.PastDue, SubscriptionStatus.Cancelled) => true,
            (SubscriptionStatus.PastDue, SubscriptionStatus.Expired) => true,
            (SubscriptionStatus.GracePeriod, SubscriptionStatus.Active) => true,
            (SubscriptionStatus.GracePeriod, SubscriptionStatus.Suspended) => true,
            (SubscriptionStatus.GracePeriod, SubscriptionStatus.Cancelled) => true,
            (SubscriptionStatus.GracePeriod, SubscriptionStatus.Expired) => true,
            (SubscriptionStatus.Suspended, SubscriptionStatus.Active) => true,
            (SubscriptionStatus.Suspended, SubscriptionStatus.Cancelled) => true,
            (SubscriptionStatus.Suspended, SubscriptionStatus.Expired) => true,
            (SubscriptionStatus.Cancelled, SubscriptionStatus.Active) => true,
            (SubscriptionStatus.Expired, SubscriptionStatus.Active) => true,
            _ => false
        };
    }

    public static bool CanExecute(SubscriptionStatus from, SubscriptionCommand command) =>
        command switch
        {
            SubscriptionCommand.RequestCancel =>
                from is SubscriptionStatus.Active
                    or SubscriptionStatus.PastDue
                    or SubscriptionStatus.GracePeriod,
            SubscriptionCommand.Activate => CanTransition(from, SubscriptionStatus.Active)
                && from is not SubscriptionStatus.Cancelled
                and not SubscriptionStatus.Expired
                and not SubscriptionStatus.Suspended,
            SubscriptionCommand.Reactivate =>
                from is SubscriptionStatus.Suspended
                    or SubscriptionStatus.Cancelled
                    or SubscriptionStatus.Expired,
            SubscriptionCommand.MarkPastDue => CanTransition(from, SubscriptionStatus.PastDue),
            SubscriptionCommand.EnterGrace => CanTransition(from, SubscriptionStatus.GracePeriod),
            SubscriptionCommand.Suspend => CanTransition(from, SubscriptionStatus.Suspended),
            SubscriptionCommand.CancelNow => CanTransition(from, SubscriptionStatus.Cancelled),
            SubscriptionCommand.Expire => CanTransition(from, SubscriptionStatus.Expired),
            _ => false
        };

    public static SubscriptionEventType? EventFor(SubscriptionCommand command) =>
        command switch
        {
            SubscriptionCommand.Activate => SubscriptionEventType.Activated,
            SubscriptionCommand.MarkPastDue => SubscriptionEventType.PaymentFailed,
            SubscriptionCommand.Suspend => SubscriptionEventType.Suspended,
            SubscriptionCommand.RequestCancel => SubscriptionEventType.CancellationRequested,
            SubscriptionCommand.CancelNow => SubscriptionEventType.Cancelled,
            SubscriptionCommand.Expire => SubscriptionEventType.Expired,
            SubscriptionCommand.Reactivate => SubscriptionEventType.Reactivated,
            _ => null
        };
}
