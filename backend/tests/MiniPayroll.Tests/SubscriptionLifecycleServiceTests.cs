using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Billing;
using MiniPayroll.Domain.Billing.Payments;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Subscriptions;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Identity;
using MiniPayroll.Infrastructure.Payments;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public sealed class SubscriptionLifecycleServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Company_admin_cannot_run_named_commands()
    {
        var fixture = await SeedAsync();
        var tenant = new StaticTenantContext
        {
            UserId = Guid.NewGuid(),
            CompanyId = fixture.CompanyId,
            IsSuperadmin = false
        };
        await using var db = TestDb.Create(tenant, fixture.Database);
        var service = new SubscriptionLifecycleService(db, tenant, fixture.Clock);

        var result = await service.ActivateAsync(fixture.CompanyId, tenant.UserId, requireAdmin: false);
        Assert.Equal(SubscriptionCommandStatus.Forbidden, result.Status);
    }

    [Fact]
    public async Task Activate_requires_a_company_admin()
    {
        var fixture = await SeedAsync(hasAdmin: false);
        var result = await fixture.Service.ActivateAsync(fixture.CompanyId, fixture.ActorId, requireAdmin: true);
        Assert.Equal(SubscriptionCommandStatus.AdminRequired, result.Status);
        Assert.Equal(SubscriptionStatus.Trialing, (await ReloadAsync(fixture)).Status);
    }

    [Fact]
    public async Task Each_named_command_writes_dates_events_and_audit()
    {
        var fixture = await SeedAsync();
        var actor = fixture.ActorId;

        await fixture.Service.ActivateAsync(fixture.CompanyId, actor, requireAdmin: true);
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        var afterActivate = await ReloadAsync(fixture);
        Assert.Equal(SubscriptionStatus.Active, afterActivate.Status);
        Assert.Equal(Now, afterActivate.CurrentPeriodStart);
        Assert.Equal(AuditActions.CompanyActivate, await LatestAuditAsync(fixture));
        Assert.Equal(SubscriptionEventType.Activated, await LatestEventAsync(fixture));

        await fixture.Service.MarkPastDueAsync(fixture.CompanyId, actor);
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(SubscriptionStatus.PastDue, (await ReloadAsync(fixture)).Status);
        Assert.Equal(afterActivate.CurrentPeriodEnd, (await ReloadAsync(fixture)).CurrentPeriodEnd);
        Assert.Equal(AuditActions.SubscriptionPastDue, await LatestAuditAsync(fixture));
        Assert.Equal(SubscriptionEventType.PaymentFailed, await LatestEventAsync(fixture));

        await fixture.Service.EnterGraceAsync(fixture.CompanyId, actor);
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(SubscriptionStatus.GracePeriod, (await ReloadAsync(fixture)).Status);
        Assert.Equal(AuditActions.SubscriptionGrace, await LatestAuditAsync(fixture));

        await fixture.Service.SuspendAsync(fixture.CompanyId, actor);
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(SubscriptionStatus.Suspended, (await ReloadAsync(fixture)).Status);
        Assert.Equal(AuditActions.SubscriptionSuspend, await LatestAuditAsync(fixture));
        Assert.Equal(SubscriptionEventType.Suspended, await LatestEventAsync(fixture));

        await fixture.Service.ReactivateAsync(fixture.CompanyId, actor);
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        var afterReactivate = await ReloadAsync(fixture);
        Assert.Equal(SubscriptionStatus.Active, afterReactivate.Status);
        Assert.Null(afterReactivate.CancelledAt);
        Assert.False(afterReactivate.CancelAtPeriodEnd);
        Assert.Equal(AuditActions.SubscriptionReactivate, await LatestAuditAsync(fixture));
        Assert.Equal(SubscriptionEventType.Reactivated, await LatestEventAsync(fixture));

        await fixture.Service.RequestCancelAsync(fixture.CompanyId, actor);
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True((await ReloadAsync(fixture)).CancelAtPeriodEnd);
        Assert.Equal(SubscriptionStatus.Active, (await ReloadAsync(fixture)).Status);
        Assert.Equal(AuditActions.SubscriptionCancel, await LatestAuditAsync(fixture));
        Assert.Equal(SubscriptionEventType.CancellationRequested, await LatestEventAsync(fixture));

        var cancelledAt = fixture.Clock.GetUtcNow();
        await fixture.Service.CancelNowAsync(fixture.CompanyId, actor);
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        var cancelled = await ReloadAsync(fixture);
        Assert.Equal(SubscriptionStatus.Cancelled, cancelled.Status);
        Assert.False(cancelled.CancelAtPeriodEnd);
        Assert.Equal(cancelledAt, cancelled.CancelledAt);
        Assert.Equal(SubscriptionEventType.Cancelled, await LatestEventAsync(fixture));

        Assert.Equal(
            SubscriptionCommandStatus.InvalidTransition,
            (await fixture.Service.MarkPastDueAsync(fixture.CompanyId, actor)).Status);

        await fixture.Service.ReactivateAsync(fixture.CompanyId, actor);
        await fixture.Service.ExpireAsync(fixture.CompanyId, actor);
        Assert.Equal(
            SubscriptionCommandStatus.InvalidTransition,
            (await fixture.Service.ExpireAsync(fixture.CompanyId, actor)).Status);

        var expiredFixture = await SeedAsync(status: SubscriptionStatus.Trialing);
        await expiredFixture.Service.ExpireAsync(expiredFixture.CompanyId, expiredFixture.ActorId);
        expiredFixture.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(SubscriptionStatus.Expired, (await ReloadAsync(expiredFixture)).Status);
        Assert.Equal(AuditActions.SubscriptionExpire, await LatestAuditAsync(expiredFixture));
        Assert.Equal(SubscriptionEventType.Expired, await LatestEventAsync(expiredFixture));
    }

    [Fact]
    public async Task Request_cancel_then_clock_after_period_end_cancels()
    {
        var fixture = await SeedAsync(status: SubscriptionStatus.Active);
        await fixture.Service.RequestCancelAsync(fixture.CompanyId, fixture.ActorId);
        Assert.True((await ReloadAsync(fixture)).CancelAtPeriodEnd);

        var later = new FrozenTimeProvider(Now.AddMonths(1));
        await using var db = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        var applied = await new SubscriptionClock(db, later).TickAsync();

        Assert.Equal(1, applied);
        var subscription = await ReloadAsync(fixture);
        Assert.Equal(SubscriptionStatus.Cancelled, subscription.Status);
        Assert.False(subscription.CancelAtPeriodEnd);
    }

    [Fact]
    public async Task Trial_clock_expires_without_deleting_company_data()
    {
        var fixture = await SeedAsync(
            status: SubscriptionStatus.Trialing,
            trialEndsAt: Now.AddDays(-1),
            withOperationalData: true);

        await using (var db = TestDb.Create(NullTenantContext.Instance, fixture.Database))
        {
            Assert.Equal(1, await new SubscriptionClock(db, fixture.Clock).TickAsync());
        }

        await using var reader = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        Assert.Equal(SubscriptionStatus.Expired, (await reader.Subscriptions.SingleAsync()).Status);
        Assert.Equal(1, await reader.Employees.CountAsync());
        Assert.Equal(1, await reader.SalaryStructures.CountAsync());
        Assert.Equal(1, await reader.PayrollRuns.CountAsync());
        Assert.Equal(1, await reader.BillingPeriods.CountAsync());
    }

    [Fact]
    public async Task Clock_does_not_emit_events_when_nothing_is_due()
    {
        var fixture = await SeedAsync(status: SubscriptionStatus.Active, nextBilling: Now.AddDays(10));
        var eventsBefore = await CountEventsAsync(fixture);

        await using var db = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        Assert.Equal(0, await new SubscriptionClock(db, fixture.Clock).TickAsync());
        Assert.Equal(eventsBefore, await CountEventsAsync(fixture));
        Assert.Equal(SubscriptionStatus.Active, (await ReloadAsync(fixture)).Status);
    }

    [Fact]
    public async Task Clock_marks_past_due_and_then_enters_grace()
    {
        var fixture = await SeedAsync(
            status: SubscriptionStatus.Active,
            nextBilling: Now.AddDays(-1));

        await using (var first = TestDb.Create(NullTenantContext.Instance, fixture.Database))
        {
            Assert.Equal(1, await new SubscriptionClock(first, fixture.Clock).TickAsync());
        }

        Assert.Equal(SubscriptionStatus.PastDue, (await ReloadAsync(fixture)).Status);

        var afterGrace = new FrozenTimeProvider(Now.AddDays(8));
        await using var second = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        Assert.Equal(1, await new SubscriptionClock(second, afterGrace).TickAsync());
        Assert.Equal(SubscriptionStatus.GracePeriod, (await ReloadAsync(fixture)).Status);
    }

    [Fact]
    public async Task Upgrade_creates_a_draft_invoice_when_the_period_has_none()
    {
        var fixture = await SeedAsync(status: SubscriptionStatus.Active);
        var growth = await AddPlanAsync(fixture.Database, "growth", 79m, 50);

        var result = await fixture.Service.ChangePlanAsync(
            fixture.CompanyId,
            growth.Code,
            null,
            fixture.ActorId);

        Assert.Equal(SubscriptionCommandStatus.Success, result.Status);
        var subscription = await ReloadAsync(fixture);
        Assert.Equal(growth.Id, subscription.PlanId);
        Assert.Equal(50, subscription.EmployeeLimit);
        Assert.Equal(SubscriptionEventType.PlanChanged, await LatestEventAsync(fixture));
        Assert.Equal(AuditActions.SubscriptionPlanChange, await LatestAuditAsync(fixture));

        await using var db = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        var invoice = Assert.Single(await db.Invoices.IgnoreQueryFilters().Include(item => item.Lines).ToListAsync());
        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
        Assert.Equal(79m, invoice.Total);
        Assert.Equal(subscription.CurrentPeriodStart, invoice.PeriodStart);
        Assert.Equal(subscription.CurrentPeriodEnd, invoice.PeriodEnd);
    }

    [Fact]
    public async Task Upgrade_does_not_mutate_a_paid_invoice()
    {
        var fixture = await SeedAsync(status: SubscriptionStatus.Active);
        var subscription = await ReloadAsync(fixture);
        var paid = InvoiceLifecycle.Create(
            fixture.CompanyId,
            subscription.Id,
            subscription.CurrentPeriodStart!.Value,
            subscription.CurrentPeriodEnd!.Value,
            1,
            49m,
            PlatformLimits.CurrencyCode,
            "Basic",
            Now).Invoice!;
        paid.Status = InvoiceStatus.Paid;
        paid.AmountPaid = paid.Total;
        paid.PaidAt = Now;
        paid.InvoiceNumber = "INV-2026-000001";
        await using (var writer = TestDb.Create(NullTenantContext.Instance, fixture.Database))
        {
            writer.Invoices.Add(paid);
            await writer.SaveChangesAsync();
        }

        await AddPlanAsync(fixture.Database, "growth", 79m, 50);
        var result = await fixture.Service.ChangePlanAsync(
            fixture.CompanyId,
            "growth",
            null,
            fixture.ActorId);

        Assert.Equal(SubscriptionCommandStatus.Success, result.Status);
        await using var db = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        var invoice = Assert.Single(await db.Invoices.IgnoreQueryFilters().ToListAsync());
        Assert.Equal(paid.Id, invoice.Id);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(49m, invoice.Total);
        Assert.Equal(49m, invoice.AmountPaid);
    }

    [Fact]
    public async Task Provider_failure_on_recurring_recreate_leaves_the_plan_unchanged()
    {
        var fixture = await SeedAsync(status: SubscriptionStatus.Active);
        var before = await ReloadAsync(fixture);
        await AddPlanAsync(fixture.Database, "growth", 79m, 50);
        await AddProviderIntentAsync(fixture.Database, fixture.CompanyId, before.Id, "sub_existing");
        var eventsBefore = await CountEventsAsync(fixture);

        var fake = new FakePaymentProvider().Next(PaymentProviderStatus.Unavailable);
        await using var db = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        var service = new SubscriptionLifecycleService(
            db,
            NullTenantContext.Instance,
            fixture.Clock,
            new PaymentGatewayService(fake));

        var result = await service.ChangePlanAsync(fixture.CompanyId, "growth", null, fixture.ActorId);

        Assert.Equal(SubscriptionCommandStatus.ProviderUnavailable, result.Status);
        var after = await ReloadAsync(fixture);
        Assert.Equal(before.PlanId, after.PlanId);
        Assert.Equal(before.EmployeeLimit, after.EmployeeLimit);
        Assert.Equal(eventsBefore, await CountEventsAsync(fixture));
        Assert.Empty(await db.Invoices.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Payment_failure_on_recurring_recreate_leaves_the_plan_unchanged()
    {
        var fixture = await SeedAsync(status: SubscriptionStatus.Active);
        var before = await ReloadAsync(fixture);
        await AddPlanAsync(fixture.Database, "growth", 79m, 50);
        await AddProviderIntentAsync(fixture.Database, fixture.CompanyId, before.Id, "sub_existing");

        var fake = new FakePaymentProvider()
            .Next(PaymentProviderStatus.Succeeded)
            .Next(PaymentProviderStatus.Failed);
        await using var db = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        var service = new SubscriptionLifecycleService(
            db,
            NullTenantContext.Instance,
            fixture.Clock,
            new PaymentGatewayService(fake));

        var result = await service.ChangePlanAsync(fixture.CompanyId, "growth", null, fixture.ActorId);

        Assert.Equal(SubscriptionCommandStatus.PaymentFailed, result.Status);
        Assert.Equal(before.PlanId, (await ReloadAsync(fixture)).PlanId);
    }

    [Fact]
    public async Task Cancel_now_cancels_provider_recurring_when_an_intent_has_a_subscription_id()
    {
        var fixture = await SeedAsync(status: SubscriptionStatus.Active);
        var subscription = await ReloadAsync(fixture);
        await AddProviderIntentAsync(fixture.Database, fixture.CompanyId, subscription.Id, "sub_live");
        var fake = new FakePaymentProvider();
        await using var db = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        var service = new SubscriptionLifecycleService(
            db,
            NullTenantContext.Instance,
            fixture.Clock,
            new PaymentGatewayService(fake));

        var result = await service.CancelNowAsync(fixture.CompanyId, fixture.ActorId);

        Assert.Equal(SubscriptionCommandStatus.Success, result.Status);
        Assert.Equal(SubscriptionStatus.Cancelled, (await ReloadAsync(fixture)).Status);
        Assert.Equal(1, fake.CancelRecurringCalls);
    }

    [Fact]
    public async Task Request_cancel_does_not_call_provider_cancel()
    {
        var fixture = await SeedAsync(status: SubscriptionStatus.Active);
        var subscription = await ReloadAsync(fixture);
        await AddProviderIntentAsync(fixture.Database, fixture.CompanyId, subscription.Id, "sub_live");
        var fake = new FakePaymentProvider();
        await using var db = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        var service = new SubscriptionLifecycleService(
            db,
            NullTenantContext.Instance,
            fixture.Clock,
            new PaymentGatewayService(fake));

        var result = await service.RequestCancelAsync(fixture.CompanyId, fixture.ActorId);

        Assert.Equal(SubscriptionCommandStatus.Success, result.Status);
        Assert.Equal(SubscriptionStatus.Active, (await ReloadAsync(fixture)).Status);
        Assert.True((await ReloadAsync(fixture)).CancelAtPeriodEnd);
        Assert.Equal(0, fake.CancelRecurringCalls);
    }

    [Fact]
    public async Task Expired_reactivate_succeeds_and_trialing_reactivate_fails()
    {
        var expired = await SeedAsync(status: SubscriptionStatus.Expired);
        var reactivated = await expired.Service.ReactivateAsync(expired.CompanyId, expired.ActorId);
        Assert.Equal(SubscriptionCommandStatus.Success, reactivated.Status);
        Assert.Equal(SubscriptionStatus.Active, (await ReloadAsync(expired)).Status);

        var trialing = await SeedAsync(status: SubscriptionStatus.Trialing);
        var rejected = await trialing.Service.ReactivateAsync(trialing.CompanyId, trialing.ActorId);
        Assert.Equal(SubscriptionCommandStatus.InvalidTransition, rejected.Status);
        Assert.Equal(SubscriptionStatus.Trialing, (await ReloadAsync(trialing)).Status);
    }

    [Fact]
    public async Task Downgrade_is_blocked_when_active_usage_exceeds_the_target_limit()
    {
        var fixture = await SeedAsync(status: SubscriptionStatus.Active);
        var before = await ReloadAsync(fixture);
        await AddPlanAsync(fixture.Database, "starter", 29m, 25);
        await AddActiveEmployeesAsync(fixture.Database, fixture.CompanyId, 40);

        var result = await fixture.Service.ChangePlanAsync(
            fixture.CompanyId,
            "starter",
            null,
            fixture.ActorId);

        Assert.Equal(SubscriptionCommandStatus.UsageExceedsPlanLimit, result.Status);
        Assert.Equal(PlanChangeRules.UsageExceededMessage, result.Error);
        var after = await ReloadAsync(fixture);
        Assert.Equal(before.PlanId, after.PlanId);
        Assert.Equal(before.EmployeeLimit, after.EmployeeLimit);
    }

    [Fact]
    public async Task Downgrade_updates_plan_and_limit_when_usage_fits()
    {
        var fixture = await SeedAsync(status: SubscriptionStatus.Active);
        var starter = await AddPlanAsync(fixture.Database, "starter", 29m, 25);
        await AddActiveEmployeesAsync(fixture.Database, fixture.CompanyId, 5);

        var result = await fixture.Service.ChangePlanAsync(
            fixture.CompanyId,
            starter.Code,
            null,
            fixture.ActorId);

        Assert.Equal(SubscriptionCommandStatus.Success, result.Status);
        var after = await ReloadAsync(fixture);
        Assert.Equal(starter.Id, after.PlanId);
        Assert.Equal(25, after.EmployeeLimit);
        Assert.Equal(SubscriptionEventType.PlanChanged, await LatestEventAsync(fixture));
    }

    private sealed record Fixture(
        string Database,
        Guid CompanyId,
        Guid ActorId,
        SubscriptionLifecycleService Service,
        FrozenTimeProvider Clock);

    private static async Task<Fixture> SeedAsync(
        SubscriptionStatus status = SubscriptionStatus.Trialing,
        bool hasAdmin = true,
        DateTimeOffset? trialEndsAt = null,
        DateTimeOffset? nextBilling = null,
        bool withOperationalData = false)
    {
        var database = $"lifecycle-{Guid.NewGuid():N}";
        var companyId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var periodEnd = new DateTimeOffset(2026, 9, 30, 23, 59, 59, TimeSpan.Zero);
        var company = new Company
        {
            Id = companyId,
            Name = "Lifecycle Co",
            ContactEmail = $"{companyId:N}@example.com",
            IsSetupComplete = true,
            SetupStep = CompanySetupStep.Complete,
            CreatedAt = Now,
            ActivatedAt = status == SubscriptionStatus.Active ? Now.AddMonths(-1) : null,
            Subscription = new Subscription
            {
                Id = subscriptionId,
                CompanyId = companyId,
                PlanId = planId,
                Status = status,
                EmployeeLimit = 10,
                GracePeriodDays = 7,
                TrialEndsAt = trialEndsAt,
                CurrentPeriodStart = status == SubscriptionStatus.Active ? Now.AddMonths(-1) : null,
                CurrentPeriodEnd = status == SubscriptionStatus.Active ? periodEnd : null,
                NextBillingDate = nextBilling ?? (status == SubscriptionStatus.Active ? periodEnd : null),
                DueDate = nextBilling ?? (status == SubscriptionStatus.Active ? periodEnd : null),
                CreatedAt = Now,
                UpdatedAt = Now
            }
        };

        await using (var writer = TestDb.Create(NullTenantContext.Instance, database))
        {
            writer.Plans.Add(new Plan
            {
                Id = planId,
                Code = "basic",
                Name = "Basic",
                IsActive = true,
                MaxActiveEmployees = 50,
                PricePerEmployee = 49m,
                DefaultEmployeeLimit = 50
            });
            writer.Companies.Add(company);
            if (hasAdmin)
            {
                writer.Users.Add(new ApplicationUser
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    Email = $"admin-{companyId:N}@example.com",
                    UserName = $"admin-{companyId:N}@example.com",
                    CreatedAt = Now
                });
            }

            if (withOperationalData)
            {
                var employeeId = Guid.NewGuid();
                writer.Employees.Add(new Employee
                {
                    Id = employeeId,
                    CompanyId = companyId,
                    EmployeeCode = "E01",
                    FullName = "Patel",
                    Phone = "9000000000",
                    Email = "patel@example.com",
                    AddressLine1 = "Street",
                    City = "Pune",
                    State = "Maharashtra",
                    PostalCode = "411001",
                    Designation = "Staff",
                    Status = EmployeeStatus.Active,
                    CreatedAt = Now
                });
                writer.SalaryStructures.Add(new SalaryStructure
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    EmployeeId = employeeId,
                    EffectiveFrom = new DateOnly(2026, 9, 1),
                    CreatedAt = Now
                });
                writer.PayrollRuns.Add(new PayrollRun
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    Year = 2026,
                    Month = 8,
                    Status = PayrollRunStatus.Finalized,
                    CreatedAt = Now
                });
                writer.BillingPeriods.Add(new BillingPeriodSnapshot
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    SubscriptionId = subscriptionId,
                    BillingPeriod = "2026-08",
                    PricePerEmployee = 49m,
                    BillableEmployees = 1,
                    BillableSource = BillableSource.ActiveHeadcount,
                    AmountDue = 49m,
                    DueDate = periodEnd
                });
            }

            await writer.SaveChangesAsync();
        }

        var clock = new FrozenTimeProvider(Now);
        var db = TestDb.Create(NullTenantContext.Instance, database);
        return new Fixture(
            database,
            companyId,
            actorId,
            new SubscriptionLifecycleService(db, NullTenantContext.Instance, clock),
            clock);
    }

    private static async Task<Subscription> ReloadAsync(Fixture fixture)
    {
        await using var db = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        return await db.Subscriptions.IgnoreQueryFilters().SingleAsync(item => item.CompanyId == fixture.CompanyId);
    }

    private static async Task<string?> LatestAuditAsync(Fixture fixture)
    {
        await using var db = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        return (await db.AuditLogs
            .IgnoreQueryFilters()
            .OrderByDescending(item => item.OccurredAt)
            .ThenByDescending(item => item.Action)
            .FirstOrDefaultAsync())?.Action;
    }

    private static async Task<SubscriptionEventType> LatestEventAsync(Fixture fixture)
    {
        await using var db = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        return (await db.SubscriptionEvents
            .IgnoreQueryFilters()
            .OrderByDescending(item => item.OccurredAt)
            .FirstAsync()).Type;
    }

    private static async Task<int> CountEventsAsync(Fixture fixture)
    {
        await using var db = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        return await db.SubscriptionEvents.IgnoreQueryFilters().CountAsync();
    }

    private static async Task<Plan> AddPlanAsync(string database, string code, decimal price, int seats)
    {
        var plan = new Plan
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = code,
            IsActive = true,
            MaxActiveEmployees = seats,
            PricePerEmployee = price,
            DefaultEmployeeLimit = seats
        };
        await using var db = TestDb.Create(NullTenantContext.Instance, database);
        db.Plans.Add(plan);
        await db.SaveChangesAsync();
        return plan;
    }

    private static async Task AddActiveEmployeesAsync(string database, Guid companyId, int count)
    {
        await using var db = TestDb.Create(NullTenantContext.Instance, database);
        for (var index = 0; index < count; index++)
        {
            db.Employees.Add(new Employee
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                EmployeeCode = $"E{index:D3}",
                FullName = $"Worker {index}",
                Phone = "9000000000",
                Email = $"w{index}@example.com",
                AddressLine1 = "Street",
                City = "Pune",
                State = "Maharashtra",
                PostalCode = "411001",
                Designation = "Staff",
                Status = EmployeeStatus.Active,
                CreatedAt = Now
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task AddProviderIntentAsync(
        string database,
        Guid companyId,
        Guid subscriptionId,
        string providerSubscriptionId)
    {
        await using var db = TestDb.Create(NullTenantContext.Instance, database);
        db.PaymentIntents.Add(new PaymentIntent
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            SubscriptionId = subscriptionId,
            Amount = 49m,
            Currency = PlatformLimits.CurrencyCode,
            IdempotencyKey = $"intent-{Guid.NewGuid():N}",
            ProviderSubscriptionId = providerSubscriptionId,
            Status = PaymentIntentStatus.Verified,
            CreatedAt = Now,
            UpdatedAt = Now
        });
        await db.SaveChangesAsync();
    }

    private sealed class FrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; private set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;

        public void Advance(TimeSpan by) => UtcNow += by;
    }
}
