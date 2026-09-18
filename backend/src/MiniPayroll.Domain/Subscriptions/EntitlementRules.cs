using MiniPayroll.Domain.Auth;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Subscriptions;

public static class EntitlementRules
{
    public static bool CanReadFeatures(SubscriptionStatus? status) =>
        status is SubscriptionStatus.Trialing
            or SubscriptionStatus.Active
            or SubscriptionStatus.PastDue
            or SubscriptionStatus.GracePeriod
            or SubscriptionStatus.Suspended;

    public static bool CanWrite(SubscriptionStatus? status) =>
        CanWrite(status, null, DateTimeOffset.UnixEpoch);

    public static bool CanWrite(
        SubscriptionStatus? status,
        DateTimeOffset? trialEndsAt,
        DateTimeOffset now) =>
        SubscriptionMutationRules.CanMutate(status, trialEndsAt, now);

    public static bool CanUseFeature(
        SubscriptionStatus? status,
        IEnumerable<PlanFeature> features,
        string featureCode)
    {
        if (!CanReadFeatures(status) || string.IsNullOrWhiteSpace(featureCode))
        {
            return false;
        }

        var catalog = features.ToList();
        if (catalog.Count == 0)
        {
            return string.Equals(featureCode, PlanFeatureCodes.Payroll, StringComparison.OrdinalIgnoreCase);
        }

        return catalog.Any(feature =>
            feature.IsEnabled
            && string.Equals(feature.Code, featureCode, StringComparison.OrdinalIgnoreCase));
    }

    public static int MaximumAllowed(int subscriptionLimit, int planMaxActiveEmployees)
    {
        var planCap = planMaxActiveEmployees > 0
            ? planMaxActiveEmployees
            : PlatformLimits.HardEmployeeCap;
        return Math.Clamp(Math.Min(subscriptionLimit, planCap), 0, PlatformLimits.HardEmployeeCap);
    }

    public static EmployeeUsage Usage(int currentUsage, int maximumAllowed, bool writable)
    {
        var remaining = Math.Max(0, maximumAllowed - currentUsage);
        return new EmployeeUsage(currentUsage, maximumAllowed, remaining, writable && remaining > 0);
    }

    public static EntitlementSnapshot Evaluate(
        SubscriptionStatus? status,
        IEnumerable<PlanFeature> features,
        int currentUsage,
        int subscriptionLimit,
        int planMaxActiveEmployees,
        DateTimeOffset? trialEndsAt = null,
        DateTimeOffset? now = null)
    {
        var clock = now ?? DateTimeOffset.UtcNow;
        var featureList = features.ToList();
        var writable = CanWrite(status, trialEndsAt, clock);
        var maximum = MaximumAllowed(subscriptionLimit, planMaxActiveEmployees);
        var usage = Usage(currentUsage, maximum, writable);
        var enabled = featureList
            .Where(feature => feature.IsEnabled && !string.IsNullOrWhiteSpace(feature.Code))
            .Select(feature => feature.Code)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new EntitlementSnapshot(
            usage,
            writable,
            writable && CanUseFeature(status, featureList, PlanFeatureCodes.Payroll),
            enabled);
    }
}
