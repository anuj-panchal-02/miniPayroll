using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Subscriptions;

namespace MiniPayroll.Tests;

public sealed class PlanChangeRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Upgrade_assigns_plan_limit_and_one_plan_changed_event()
    {
        var current = NewPlan("basic", 49m, 25);
        var target = NewPlan("growth", 79m, 50);
        var subscription = LiveOn(current, SubscriptionStatus.Active, employeeLimit: 10);

        Assert.Equal(PlanChangeKind.Upgrade, PlanChangeRules.Direction(current, target, BillingCycle.Monthly, Now));
        Assert.Equal(
            PlanChangeStatus.Success,
            PlanChangeRules.Evaluate(subscription, target, currentUsage: 8, Now));

        var assigned = SubscriptionLifecycle.AssignPlan(
            subscription,
            target,
            null,
            Now,
            employeeLimit: PlanChangeRules.EmployeeLimitFor(target));

        Assert.Equal(SubscriptionLifecycleStatus.Success, assigned.Status);
        Assert.Equal(target.Id, subscription.PlanId);
        Assert.Equal(50, subscription.EmployeeLimit);
        Assert.Equal(SubscriptionEventType.PlanChanged, Assert.Single(assigned.Events!).Type);
    }

    [Fact]
    public void Downgrade_is_allowed_when_usage_fits()
    {
        var current = NewPlan("growth", 79m, 50);
        var target = NewPlan("starter", 29m, 25);
        var subscription = LiveOn(current, SubscriptionStatus.Active, employeeLimit: 50);

        Assert.Equal(PlanChangeKind.Downgrade, PlanChangeRules.Direction(current, target, BillingCycle.Monthly, Now));
        Assert.Equal(
            PlanChangeStatus.Success,
            PlanChangeRules.Evaluate(subscription, target, currentUsage: 20, Now));
        Assert.Equal(25, PlanChangeRules.EmployeeLimitFor(target));
    }

    [Fact]
    public void Downgrade_with_usage_over_the_target_cap_is_rejected()
    {
        var current = NewPlan("growth", 79m, 50);
        var target = NewPlan("starter", 29m, 25);
        var subscription = LiveOn(current, SubscriptionStatus.Active, employeeLimit: 50);
        var planId = subscription.PlanId;
        var limit = subscription.EmployeeLimit;

        var status = PlanChangeRules.Evaluate(subscription, target, currentUsage: 40, Now);

        Assert.Equal(PlanChangeStatus.UsageExceedsPlanLimit, status);
        Assert.Equal(PlanChangeRules.UsageExceededMessage, "Current usage exceeds target plan limit.");
        Assert.Equal(planId, subscription.PlanId);
        Assert.Equal(limit, subscription.EmployeeLimit);
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
    }

    [Fact]
    public void Same_plan_is_a_no_op()
    {
        var plan = NewPlan("basic", 49m, 50);
        var subscription = LiveOn(plan, SubscriptionStatus.Active);

        Assert.Equal(PlanChangeStatus.SamePlan, PlanChangeRules.Evaluate(subscription, plan, 3, Now));
        Assert.Equal(PlanChangeKind.Same, PlanChangeRules.Direction(plan, plan, BillingCycle.Monthly, Now));
    }

    [Fact]
    public void Rejects_inactive_plan_and_terminal_statuses()
    {
        var current = NewPlan("basic", 49m, 50);
        var inactive = NewPlan("growth", 79m, 50);
        inactive.IsActive = false;
        var cancelled = LiveOn(current, SubscriptionStatus.Cancelled);

        Assert.Equal(PlanChangeStatus.PlanInactive, PlanChangeRules.Evaluate(LiveOn(current), inactive, 1, Now));
        Assert.Equal(PlanChangeStatus.PlanNotFound, PlanChangeRules.Evaluate(LiveOn(current), null, 1, Now));
        Assert.Equal(PlanChangeStatus.InvalidStatus, PlanChangeRules.Evaluate(cancelled, NewPlan("growth", 79m, 50), 1, Now));
        Assert.False(PlanChangeRules.CanChange(SubscriptionStatus.Cancelled));
        Assert.False(PlanChangeRules.CanChange(SubscriptionStatus.Expired));
        Assert.False(PlanChangeRules.CanChange(SubscriptionStatus.Suspended));
        Assert.True(PlanChangeRules.CanChange(SubscriptionStatus.PastDue));
        Assert.True(PlanChangeRules.CanChange(SubscriptionStatus.GracePeriod));
    }

    private static Subscription LiveOn(
        Plan plan,
        SubscriptionStatus status = SubscriptionStatus.Active,
        int employeeLimit = 10) =>
        new()
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            PlanId = plan.Id,
            Plan = plan,
            Status = status,
            EmployeeLimit = employeeLimit,
            BillingCycle = BillingCycle.Monthly,
            CreatedAt = Now,
            UpdatedAt = Now
        };

    private static Plan NewPlan(string code, decimal price, int seats) =>
        new()
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = code,
            IsActive = true,
            MaxActiveEmployees = seats,
            PricePerEmployee = price,
            DefaultEmployeeLimit = seats
        };
}
