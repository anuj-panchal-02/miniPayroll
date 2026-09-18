using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Subscriptions;

public static class SubscriptionStatuses
{
    public static bool IsLive(SubscriptionStatus status) =>
        status is SubscriptionStatus.Trialing
            or SubscriptionStatus.Active
            or SubscriptionStatus.PastDue
            or SubscriptionStatus.GracePeriod
            or SubscriptionStatus.Suspended;

    public static bool IsTerminal(SubscriptionStatus status) =>
        status is SubscriptionStatus.Cancelled or SubscriptionStatus.Expired;
}
