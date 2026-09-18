using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Subscriptions;
using MiniPayroll.Domain.Tenancy;

namespace MiniPayroll.Tests;

public sealed class SubscriptionDomainTests
{
    [Fact]
    public async Task Creates_a_trialing_subscription_on_the_assigned_plan()
    {
        var fixture = await SeedAsync();
        var now = new DateTimeOffset(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);

        await using var db = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        var created = SubscriptionLifecycle.Create(
            fixture.CompanyId,
            fixture.Plan,
            employeeLimit: 20,
            BillingCycle.Monthly,
            now,
            []);

        Assert.Equal(SubscriptionLifecycleStatus.Success, created.Status);
        Assert.Equal(fixture.Plan.Id, created.Subscription!.PlanId);
        Assert.Equal(SubscriptionStatus.Trialing, created.Subscription.Status);
        Assert.Equal(BillingCycle.Monthly, created.Subscription.BillingCycle);
        Assert.Equal(20, created.Subscription.EmployeeLimit);
        Assert.Equal(now, created.Subscription.CreatedAt);
        Assert.Null(created.Subscription.TrialStartedAt);
        Assert.Empty(created.Events!);

        db.Subscriptions.Add(created.Subscription);
        await db.SaveChangesAsync();
        Assert.Equal(1, await db.Subscriptions.CountAsync(item => item.CompanyId == fixture.CompanyId));
    }

    [Fact]
    public void Assigns_a_plan_by_code_not_name()
    {
        var starter = NewPlan("starter", "Starter", 29m);
        var growth = NewPlan("growth", "Growth", 79m);
        var now = new DateTimeOffset(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);
        var created = SubscriptionLifecycle.Create(
            Guid.NewGuid(),
            starter,
            10,
            BillingCycle.Monthly,
            now,
            []);

        var assigned = SubscriptionLifecycle.AssignPlan(
            created.Subscription!,
            growth,
            BillingCycle.Yearly,
            now.AddMinutes(1));

        Assert.Equal(SubscriptionLifecycleStatus.Success, assigned.Status);
        Assert.Equal(growth.Id, assigned.Subscription!.PlanId);
        Assert.Equal(BillingCycle.Yearly, assigned.Subscription.BillingCycle);
        Assert.Equal(SubscriptionEventType.PlanChanged, Assert.Single(assigned.Events!).Type);
        Assert.NotEqual("Starter", growth.Code);
        Assert.Equal("growth", growth.Code);
    }

    [Fact]
    public void Assign_plan_sets_the_computed_employee_limit()
    {
        var starter = NewPlan("starter", "Starter", 29m);
        starter.MaxActiveEmployees = 25;
        starter.DefaultEmployeeLimit = 25;
        var created = SubscriptionLifecycle.Create(
            Guid.NewGuid(),
            starter,
            10,
            BillingCycle.Monthly,
            DateTimeOffset.UtcNow,
            []);
        var growth = NewPlan("growth", "Growth", 79m);
        growth.MaxActiveEmployees = 50;
        growth.DefaultEmployeeLimit = 50;

        var assigned = SubscriptionLifecycle.AssignPlan(
            created.Subscription!,
            growth,
            null,
            DateTimeOffset.UtcNow,
            employeeLimit: PlanChangeRules.EmployeeLimitFor(growth));

        Assert.Equal(50, assigned.Subscription!.EmployeeLimit);
        Assert.Equal(growth.Id, assigned.Subscription.PlanId);
    }

    [Fact]
    public void Cancel_at_period_end_keeps_access_until_cancel_now()
    {
        var now = new DateTimeOffset(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);
        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            PlanId = Guid.NewGuid(),
            Status = SubscriptionStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        var requested = SubscriptionLifecycle.Execute(subscription, SubscriptionCommand.RequestCancel, now);
        Assert.Equal(SubscriptionLifecycleStatus.Success, requested.Status);
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.True(subscription.CancelAtPeriodEnd);

        var cancelled = SubscriptionLifecycle.Execute(subscription, SubscriptionCommand.CancelNow, now.AddDays(1));
        Assert.Equal(SubscriptionStatus.Cancelled, cancelled.Subscription!.Status);
        Assert.False(cancelled.Subscription.CancelAtPeriodEnd);
    }

    [Fact]
    public void Reactivate_from_cancelled_or_expired_returns_active()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var from in new[] { SubscriptionStatus.Cancelled, SubscriptionStatus.Expired })
        {
            var subscription = new Subscription
            {
                Id = Guid.NewGuid(),
                CompanyId = Guid.NewGuid(),
                PlanId = Guid.NewGuid(),
                Status = from,
                CreatedAt = now,
                UpdatedAt = now
            };
            var result = SubscriptionLifecycle.Execute(subscription, SubscriptionCommand.Reactivate, now);
            Assert.Equal(SubscriptionLifecycleStatus.Success, result.Status);
            Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        }

        Assert.Equal(
            SubscriptionLifecycleStatus.InvalidTransition,
            SubscriptionLifecycle.Execute(
                new Subscription
                {
                    Id = Guid.NewGuid(),
                    CompanyId = Guid.NewGuid(),
                    PlanId = Guid.NewGuid(),
                    Status = SubscriptionStatus.Trialing,
                    CreatedAt = now,
                    UpdatedAt = now
                },
                SubscriptionCommand.Reactivate,
                now).Status);
    }

    [Fact]
    public void Resolves_monthly_and_yearly_prices_independently()
    {
        var plan = NewPlan("basic", "Basic", 49m);
        var now = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        PlanPricing.Revise(plan, BillingCycle.Monthly, 49m, now);
        PlanPricing.Revise(plan, BillingCycle.Yearly, 490m, now);

        Assert.Equal(49m, PlanPricing.Current(plan.Prices, BillingCycle.Monthly, now)!.Amount);
        Assert.Equal(490m, PlanPricing.Current(plan.Prices, BillingCycle.Yearly, now)!.Amount);
        Assert.Equal("INR", PlanPricing.Current(plan.Prices, BillingCycle.Yearly, now)!.Currency);
    }

    [Fact]
    public void Price_history_keeps_the_closed_row()
    {
        var plan = NewPlan("basic", "Basic", 49m);
        var firstOn = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var secondOn = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);
        PlanPricing.Revise(plan, BillingCycle.Monthly, 49m, firstOn);
        PlanPricing.Revise(plan, BillingCycle.Monthly, 59m, secondOn);

        var historic = PlanPricing.Current(plan.Prices, BillingCycle.Monthly, firstOn.AddDays(10));
        var current = PlanPricing.Current(plan.Prices, BillingCycle.Monthly, secondOn.AddDays(1));
        Assert.Equal(49m, historic!.Amount);
        Assert.False(historic.IsActive);
        Assert.Equal(firstOn, historic.EffectiveFrom);
        Assert.Equal(secondOn, historic.EffectiveTo);
        Assert.Equal(59m, current!.Amount);
        Assert.True(current.IsActive);
        Assert.Equal(59m, plan.PricePerEmployee);
        Assert.Equal(2, plan.Prices.Count);
    }

    [Theory]
    [InlineData(SubscriptionStatus.Trialing, SubscriptionStatus.Active, true)]
    [InlineData(SubscriptionStatus.Active, SubscriptionStatus.PastDue, true)]
    [InlineData(SubscriptionStatus.PastDue, SubscriptionStatus.GracePeriod, true)]
    [InlineData(SubscriptionStatus.GracePeriod, SubscriptionStatus.Suspended, true)]
    [InlineData(SubscriptionStatus.Suspended, SubscriptionStatus.Active, true)]
    [InlineData(SubscriptionStatus.Active, SubscriptionStatus.Cancelled, true)]
    [InlineData(SubscriptionStatus.Cancelled, SubscriptionStatus.Active, true)]
    [InlineData(SubscriptionStatus.Expired, SubscriptionStatus.Active, true)]
    [InlineData(SubscriptionStatus.Trialing, SubscriptionStatus.Suspended, false)]
    [InlineData(SubscriptionStatus.GracePeriod, SubscriptionStatus.PastDue, false)]
    [InlineData(SubscriptionStatus.Cancelled, SubscriptionStatus.PastDue, false)]
    public void Status_transitions_follow_the_lifecycle_matrix(
        SubscriptionStatus from,
        SubscriptionStatus to,
        bool allowed)
    {
        Assert.Equal(allowed, SubscriptionStatusTransitions.CanTransition(from, to));
        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            PlanId = Guid.NewGuid(),
            Status = from,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        var result = SubscriptionLifecycle.Transition(subscription, to, DateTimeOffset.UtcNow);
        Assert.Equal(
            allowed ? SubscriptionLifecycleStatus.Success : SubscriptionLifecycleStatus.InvalidTransition,
            result.Status);
        if (allowed && from != to)
        {
            Assert.Equal(to, subscription.Status);
        }
    }

    [Fact]
    public void Exhaustive_from_to_matrix_matches_the_command_graph()
    {
        HashSet<(SubscriptionStatus From, SubscriptionStatus To)> allowed =
        [
            (SubscriptionStatus.Trialing, SubscriptionStatus.Active),
            (SubscriptionStatus.Trialing, SubscriptionStatus.Expired),
            (SubscriptionStatus.Trialing, SubscriptionStatus.Cancelled),
            (SubscriptionStatus.Active, SubscriptionStatus.PastDue),
            (SubscriptionStatus.Active, SubscriptionStatus.GracePeriod),
            (SubscriptionStatus.Active, SubscriptionStatus.Suspended),
            (SubscriptionStatus.Active, SubscriptionStatus.Cancelled),
            (SubscriptionStatus.PastDue, SubscriptionStatus.Active),
            (SubscriptionStatus.PastDue, SubscriptionStatus.GracePeriod),
            (SubscriptionStatus.PastDue, SubscriptionStatus.Suspended),
            (SubscriptionStatus.PastDue, SubscriptionStatus.Cancelled),
            (SubscriptionStatus.PastDue, SubscriptionStatus.Expired),
            (SubscriptionStatus.GracePeriod, SubscriptionStatus.Active),
            (SubscriptionStatus.GracePeriod, SubscriptionStatus.Suspended),
            (SubscriptionStatus.GracePeriod, SubscriptionStatus.Expired),
            (SubscriptionStatus.GracePeriod, SubscriptionStatus.Cancelled),
            (SubscriptionStatus.Suspended, SubscriptionStatus.Active),
            (SubscriptionStatus.Suspended, SubscriptionStatus.Cancelled),
            (SubscriptionStatus.Suspended, SubscriptionStatus.Expired),
            (SubscriptionStatus.Cancelled, SubscriptionStatus.Active),
            (SubscriptionStatus.Expired, SubscriptionStatus.Active)
        ];

        foreach (var from in Enum.GetValues<SubscriptionStatus>())
        {
            foreach (var to in Enum.GetValues<SubscriptionStatus>())
            {
                var expected = from == to || allowed.Contains((from, to));
                Assert.Equal(expected, SubscriptionStatusTransitions.CanTransition(from, to));

                var subscription = new Subscription
                {
                    Id = Guid.NewGuid(),
                    CompanyId = Guid.NewGuid(),
                    PlanId = Guid.NewGuid(),
                    Status = from,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow
                };
                var result = SubscriptionLifecycle.Transition(subscription, to, DateTimeOffset.UtcNow);
                Assert.Equal(
                    expected ? SubscriptionLifecycleStatus.Success : SubscriptionLifecycleStatus.InvalidTransition,
                    result.Status);
            }
        }
    }

    [Fact]
    public void Named_commands_update_dates_and_events()
    {
        var now = new DateTimeOffset(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);
        var periodEnd = new DateTimeOffset(2026, 9, 30, 23, 59, 59, TimeSpan.Zero);
        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            PlanId = Guid.NewGuid(),
            Status = SubscriptionStatus.Trialing,
            CreatedAt = now,
            UpdatedAt = now
        };

        var activated = SubscriptionLifecycle.Execute(subscription, SubscriptionCommand.Activate, now);
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.Equal(now, subscription.CurrentPeriodStart);
        Assert.Equal(periodEnd, subscription.CurrentPeriodEnd);
        Assert.Equal(periodEnd, subscription.NextBillingDate);
        Assert.Equal(SubscriptionEventType.Activated, Assert.Single(activated.Events!).Type);

        var pastDue = SubscriptionLifecycle.Execute(subscription, SubscriptionCommand.MarkPastDue, now.AddDays(1));
        Assert.Equal(SubscriptionStatus.PastDue, subscription.Status);
        Assert.Equal(periodEnd, subscription.CurrentPeriodEnd);
        Assert.Equal(SubscriptionEventType.PaymentFailed, Assert.Single(pastDue.Events!).Type);

        var grace = SubscriptionLifecycle.Execute(subscription, SubscriptionCommand.EnterGrace, now.AddDays(2));
        Assert.Equal(SubscriptionStatus.GracePeriod, subscription.Status);
        Assert.Equal(periodEnd, subscription.CurrentPeriodEnd);
        Assert.Empty(grace.Events!);

        var suspended = SubscriptionLifecycle.Execute(subscription, SubscriptionCommand.Suspend, now.AddDays(3));
        Assert.Equal(SubscriptionStatus.Suspended, subscription.Status);
        Assert.Equal(SubscriptionEventType.Suspended, Assert.Single(suspended.Events!).Type);

        var reactivated = SubscriptionLifecycle.Execute(
            subscription,
            SubscriptionCommand.Reactivate,
            now.AddMonths(1));
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.Null(subscription.CancelledAt);
        Assert.False(subscription.CancelAtPeriodEnd);
        Assert.Equal(now.AddMonths(1), subscription.CurrentPeriodStart);
        Assert.Equal(SubscriptionEventType.Reactivated, Assert.Single(reactivated.Events!).Type);

        var requested = SubscriptionLifecycle.Execute(
            subscription,
            SubscriptionCommand.RequestCancel,
            now.AddMonths(1).AddHours(1));
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
        Assert.True(subscription.CancelAtPeriodEnd);
        Assert.Equal(SubscriptionEventType.CancellationRequested, Assert.Single(requested.Events!).Type);

        var cancelled = SubscriptionLifecycle.Execute(
            subscription,
            SubscriptionCommand.CancelNow,
            now.AddMonths(1).AddHours(2));
        Assert.Equal(SubscriptionStatus.Cancelled, subscription.Status);
        Assert.False(subscription.CancelAtPeriodEnd);
        Assert.Equal(now.AddMonths(1).AddHours(2), subscription.CancelledAt);
        Assert.Equal(SubscriptionEventType.Cancelled, Assert.Single(cancelled.Events!).Type);

        Assert.Equal(
            SubscriptionLifecycleStatus.InvalidTransition,
            SubscriptionLifecycle.Execute(subscription, SubscriptionCommand.MarkPastDue, now).Status);

        var expired = SubscriptionLifecycle.Execute(
            new Subscription
            {
                Id = Guid.NewGuid(),
                CompanyId = Guid.NewGuid(),
                PlanId = Guid.NewGuid(),
                Status = SubscriptionStatus.Trialing,
                TrialEndsAt = now.AddDays(-1),
                CurrentPeriodEnd = periodEnd,
                CreatedAt = now,
                UpdatedAt = now
            },
            SubscriptionCommand.Expire,
            now);
        Assert.Equal(SubscriptionStatus.Expired, expired.Subscription!.Status);
        Assert.Equal(periodEnd, expired.Subscription.CurrentPeriodEnd);
        Assert.Equal(SubscriptionEventType.Expired, Assert.Single(expired.Events!).Type);
    }

    [Fact]
    public void Clock_selects_due_commands_and_stays_quiet_when_nothing_is_due()
    {
        var now = new DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(
            SubscriptionCommand.Expire,
            SubscriptionClockRules.DueCommand(
                new Subscription
                {
                    Status = SubscriptionStatus.Trialing,
                    TrialEndsAt = now.AddHours(-1)
                },
                now));
        Assert.Equal(
            SubscriptionCommand.CancelNow,
            SubscriptionClockRules.DueCommand(
                new Subscription
                {
                    Status = SubscriptionStatus.Active,
                    CancelAtPeriodEnd = true,
                    CurrentPeriodEnd = now.AddHours(-1)
                },
                now));
        Assert.Equal(
            SubscriptionCommand.MarkPastDue,
            SubscriptionClockRules.DueCommand(
                new Subscription
                {
                    Status = SubscriptionStatus.Active,
                    NextBillingDate = now.AddHours(-1)
                },
                now));
        Assert.Equal(
            SubscriptionCommand.EnterGrace,
            SubscriptionClockRules.DueCommand(
                new Subscription
                {
                    Status = SubscriptionStatus.PastDue,
                    DueDate = now.AddDays(-8),
                    GracePeriodDays = 7
                },
                now));
        Assert.Null(
            SubscriptionClockRules.DueCommand(
                new Subscription
                {
                    Status = SubscriptionStatus.Active,
                    NextBillingDate = now.AddDays(1),
                    CurrentPeriodEnd = now.AddDays(10)
                },
                now));
        Assert.Null(
            SubscriptionClockRules.DueCommand(
                new Subscription
                {
                    Status = SubscriptionStatus.GracePeriod,
                    DueDate = now.AddDays(-30),
                    GracePeriodDays = 7
                },
                now));
    }

    [Fact]
    public void Rejects_a_second_live_subscription_for_the_same_company()
    {
        var plan = NewPlan("basic", "Basic", 49m);
        var companyId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var first = SubscriptionLifecycle.Create(companyId, plan, 10, BillingCycle.Monthly, now, []);
        Assert.Equal(SubscriptionLifecycleStatus.Success, first.Status);

        var duplicate = SubscriptionLifecycle.Create(
            companyId,
            plan,
            10,
            BillingCycle.Monthly,
            now,
            [first.Subscription!]);

        Assert.Equal(SubscriptionLifecycleStatus.DuplicateLiveSubscription, duplicate.Status);
        Assert.Null(duplicate.Subscription);
    }

    [Fact]
    public async Task Company_isolation_hides_foreign_subscriptions_and_events()
    {
        var database = $"sub-iso-{Guid.NewGuid():N}";
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();
        var plan = NewPlan("basic", "Basic", 49m);
        var now = DateTimeOffset.UtcNow;
        var first = SubscriptionLifecycle.Create(companyA, plan, 8, BillingCycle.Monthly, now, []);
        var second = SubscriptionLifecycle.Create(companyB, plan, 8, BillingCycle.Monthly, now, []);
        SubscriptionLifecycle.Activate(first.Subscription!, now);
        SubscriptionLifecycle.Record(first.Subscription!, SubscriptionEventType.Activated, now);

        await using (var writer = TestDb.Create(NullTenantContext.Instance, database))
        {
            writer.Plans.Add(plan);
            writer.Companies.Add(NewCompany(companyA, "A"));
            writer.Companies.Add(NewCompany(companyB, "B"));
            writer.Subscriptions.Add(first.Subscription!);
            writer.Subscriptions.Add(second.Subscription!);
            writer.SubscriptionEvents.AddRange(first.Subscription!.Events);
            await writer.SaveChangesAsync();
        }

        var tenantA = new StaticTenantContext { UserId = Guid.NewGuid(), CompanyId = companyA };
        await using var db = TestDb.Create(tenantA, database);
        Assert.Equal(companyA, Assert.Single(await db.Subscriptions.ToListAsync()).CompanyId);
        Assert.All(await db.SubscriptionEvents.ToListAsync(), item => Assert.Equal(companyA, item.CompanyId));
        Assert.Empty(await db.Subscriptions.Where(item => item.CompanyId == companyB).ToListAsync());
    }

    [Fact]
    public void Preserves_production_like_plan_and_subscription_defaults()
    {
        const string planName = "Basic";
        const decimal price = 49m;
        const int defaultLimit = 50;
        const bool isPublic = true;
        var companyCreatedAt = new DateTimeOffset(2026, 9, 11, 8, 12, 56, TimeSpan.Zero);
        var periodEnd = new DateTimeOffset(2026, 9, 30, 23, 59, 59, TimeSpan.Zero);

        Assert.Equal(PlatformLimits.DefaultPlanCode, SubscriptionSchemaBackfill.PlanCodeFromName(planName));
        Assert.Equal(defaultLimit, SubscriptionSchemaBackfill.MaxActiveEmployeesFromLegacy(defaultLimit));
        Assert.True(SubscriptionSchemaBackfill.IsActiveFromLegacy(isPublic));
        Assert.Equal(price, PlatformLimits.DefaultPricePerEmployee);
        Assert.Equal(
            periodEnd,
            SubscriptionSchemaBackfill.NextBillingDateFromLegacy(null, periodEnd));
        Assert.Equal(
            companyCreatedAt,
            SubscriptionSchemaBackfill.CreatedAtFromLegacy(companyCreatedAt, companyCreatedAt.AddMinutes(1)));
        Assert.Equal(0, (int)SubscriptionStatus.Trialing);
        Assert.Equal(1, (int)SubscriptionStatus.Active);
        Assert.Equal(2, (int)SubscriptionStatus.PastDue);
        Assert.Equal(3, (int)SubscriptionStatus.Suspended);
        Assert.Equal(4, (int)SubscriptionStatus.Cancelled);
    }

    [Fact]
    public void Records_trial_start_from_plan_configuration_not_the_plan_name()
    {
        var plan = NewPlan("growth", "Growth", 79m);
        plan.TrialDays = 14;
        var now = new DateTimeOffset(2026, 9, 12, 8, 0, 0, TimeSpan.Zero);
        var created = SubscriptionLifecycle.Create(
            Guid.NewGuid(),
            plan,
            5,
            BillingCycle.Monthly,
            now,
            []);

        Assert.Equal(now, created.Subscription!.TrialStartedAt);
        Assert.Equal(now.AddDays(14), created.Subscription.TrialEndsAt);
        Assert.Equal(SubscriptionEventType.TrialStarted, Assert.Single(created.Events!).Type);
    }

    private sealed record Seed(string Database, Guid CompanyId, Plan Plan);

    private static async Task<Seed> SeedAsync()
    {
        var database = $"sub-{Guid.NewGuid():N}";
        var companyId = Guid.NewGuid();
        var plan = NewPlan(PlatformLimits.DefaultPlanCode, PlatformLimits.DefaultPlanName, 49m);
        await using var db = TestDb.Create(NullTenantContext.Instance, database);
        db.Plans.Add(plan);
        db.Companies.Add(NewCompany(companyId, "Dolphin Group"));
        await db.SaveChangesAsync();
        return new Seed(database, companyId, plan);
    }

    private static Plan NewPlan(string code, string name, decimal monthlyPrice) =>
        new()
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = name,
            IsActive = true,
            MaxActiveEmployees = PlatformLimits.DefaultEmployeeLimit,
            TrialDays = 0,
            PricePerEmployee = monthlyPrice,
            DefaultEmployeeLimit = PlatformLimits.DefaultEmployeeLimit,
            IsPublic = true
        };

    private static Company NewCompany(Guid id, string name) =>
        new()
        {
            Id = id,
            Name = name,
            ContactEmail = $"{id:N}@example.com",
            CreatedAt = DateTimeOffset.UtcNow
        };
}
