using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Auth;

public static class CompanyAdminLoginAccess
{
    public static bool IsAllowed(SubscriptionStatus? status) =>
        IsAllowed(status, null, DateTimeOffset.UnixEpoch);

    public static bool IsAllowed(
        SubscriptionStatus? status,
        DateTimeOffset? trialEndsAt,
        DateTimeOffset now) =>
        status is SubscriptionStatus.Active
            or SubscriptionStatus.PastDue
            or SubscriptionStatus.GracePeriod
            or SubscriptionStatus.Suspended
            || IsOpenTrial(status, trialEndsAt, now);

    public static bool IsOpenTrial(
        SubscriptionStatus? status,
        DateTimeOffset? trialEndsAt,
        DateTimeOffset now) =>
        status == SubscriptionStatus.Trialing
        && trialEndsAt is { } end
        && end >= now;
}
