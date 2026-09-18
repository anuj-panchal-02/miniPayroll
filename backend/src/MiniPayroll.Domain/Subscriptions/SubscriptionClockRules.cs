using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Subscriptions;

public static class SubscriptionClockRules
{
    public static SubscriptionCommand? DueCommand(Subscription subscription, DateTimeOffset now)
    {
        if (subscription.Status == SubscriptionStatus.Trialing
            && subscription.TrialEndsAt is { } trialEnd
            && trialEnd < now)
        {
            return SubscriptionCommand.Expire;
        }

        if (subscription.Status == SubscriptionStatus.Active
            && subscription.CancelAtPeriodEnd
            && subscription.CurrentPeriodEnd is { } periodEnd
            && periodEnd < now)
        {
            return SubscriptionCommand.CancelNow;
        }

        var due = subscription.NextBillingDate ?? subscription.DueDate;
        if (subscription.Status == SubscriptionStatus.Active
            && !subscription.CancelAtPeriodEnd
            && due is { } billingDue
            && billingDue < now)
        {
            return SubscriptionCommand.MarkPastDue;
        }

        if (subscription.Status == SubscriptionStatus.PastDue
            && due is { } pastDue
            && pastDue.AddDays(Math.Max(subscription.GracePeriodDays, 0)) < now)
        {
            return SubscriptionCommand.EnterGrace;
        }

        return null;
    }
}
