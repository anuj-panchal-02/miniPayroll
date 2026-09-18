using MiniPayroll.Domain.Auth;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Subscriptions;

public enum PlanChangeStatus
{
    Success,
    SamePlan,
    PlanNotFound,
    PlanInactive,
    InvalidStatus,
    UsageExceedsPlanLimit
}

public enum PlanChangeKind
{
    Same,
    Upgrade,
    Downgrade,
    Lateral
}

public static class PlanChangeRules
{
    public const string UsageExceededMessage = "Current usage exceeds target plan limit.";

    public static bool CanChange(SubscriptionStatus status) =>
        status is SubscriptionStatus.Trialing
            or SubscriptionStatus.Active
            or SubscriptionStatus.PastDue
            or SubscriptionStatus.GracePeriod;

    public static int EmployeeLimitFor(Plan plan)
    {
        var preferred = plan.DefaultEmployeeLimit > 0
            ? plan.DefaultEmployeeLimit
            : plan.MaxActiveEmployees;
        var planCap = plan.MaxActiveEmployees > 0
            ? plan.MaxActiveEmployees
            : PlatformLimits.HardEmployeeCap;
        var limited = Math.Min(preferred, planCap);
        return Math.Clamp(limited, PlatformLimits.MinEmployeeLimit, PlatformLimits.HardEmployeeCap);
    }

    public static PlanChangeKind Direction(
        Plan current,
        Plan target,
        BillingCycle cycle,
        DateTimeOffset now)
    {
        if (current.Id == target.Id)
        {
            return PlanChangeKind.Same;
        }

        var currentPrice = PlanPricing.AmountOrFallback(
            current.Prices,
            cycle,
            now,
            current.PricePerEmployee);
        var targetPrice = PlanPricing.AmountOrFallback(
            target.Prices,
            cycle,
            now,
            target.PricePerEmployee);
        var higherPrice = targetPrice > currentPrice;
        var lowerPrice = targetPrice < currentPrice;
        var higherCap = target.MaxActiveEmployees > current.MaxActiveEmployees;
        var lowerCap = target.MaxActiveEmployees < current.MaxActiveEmployees;

        if (higherPrice || higherCap)
        {
            return PlanChangeKind.Upgrade;
        }

        if (lowerPrice || lowerCap)
        {
            return PlanChangeKind.Downgrade;
        }

        return PlanChangeKind.Lateral;
    }

    public static PlanChangeStatus Evaluate(
        Subscription subscription,
        Plan? target,
        int currentUsage,
        DateTimeOffset now,
        BillingCycle? billingCycle = null)
    {
        if (target is null)
        {
            return PlanChangeStatus.PlanNotFound;
        }

        var cycle = billingCycle ?? subscription.BillingCycle;
        if (subscription.PlanId == target.Id && cycle == subscription.BillingCycle)
        {
            return PlanChangeStatus.SamePlan;
        }

        if (!target.IsActive)
        {
            return PlanChangeStatus.PlanInactive;
        }

        if (!CanChange(subscription.Status))
        {
            return PlanChangeStatus.InvalidStatus;
        }

        var employeeLimit = EmployeeLimitFor(target);
        if (!EmployeeLimitRules.IsValid(employeeLimit))
        {
            return PlanChangeStatus.InvalidStatus;
        }

        var maximum = EntitlementRules.MaximumAllowed(employeeLimit, target.MaxActiveEmployees);
        return currentUsage > maximum
            ? PlanChangeStatus.UsageExceedsPlanLimit
            : PlanChangeStatus.Success;
    }
}
