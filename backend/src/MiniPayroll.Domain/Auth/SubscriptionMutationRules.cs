using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Auth;

public static class SubscriptionMutationRules
{
    public static bool CanMutate(SubscriptionStatus? status) =>
        CanMutate(status, null, DateTimeOffset.UnixEpoch);

    public static bool CanMutate(
        SubscriptionStatus? status,
        DateTimeOffset? trialEndsAt,
        DateTimeOffset now) =>
        status is SubscriptionStatus.Active
            or SubscriptionStatus.PastDue
            or SubscriptionStatus.GracePeriod
            || CompanyAdminLoginAccess.IsOpenTrial(status, trialEndsAt, now);
}
