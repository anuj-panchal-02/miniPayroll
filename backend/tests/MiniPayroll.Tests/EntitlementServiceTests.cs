using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Subscriptions;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public sealed class EntitlementServiceTests
{
    [Fact]
    public async Task Paid_plan_can_run_payroll_and_add_within_the_limit()
    {
        var fixture = await SeedAsync(code: "paid", payrollEnabled: true, limit: 25, activeEmployees: 18);
        await using var _ = fixture.Db;

        var usage = await fixture.Entitlements.GetActiveEmployeeUsage(fixture.CompanyId);
        Assert.Equal(18, usage.CurrentUsage);
        Assert.Equal(25, usage.MaximumAllowed);
        Assert.Equal(7, usage.Remaining);
        Assert.True(usage.CanAdd);
        Assert.True(await fixture.Entitlements.CanAddEmployee(fixture.CompanyId));
        Assert.True(await fixture.Entitlements.CanRunPayroll(fixture.CompanyId));
        Assert.True(await fixture.Entitlements.CanUseFeature(fixture.CompanyId, PlanFeatureCodes.Payroll));
        Assert.Equal(25, await fixture.Entitlements.GetEmployeeLimit(fixture.CompanyId));
    }

    [Fact]
    public async Task Free_plan_cannot_run_payroll_when_the_feature_is_off()
    {
        var fixture = await SeedAsync(code: "free", payrollEnabled: false, limit: 3, activeEmployees: 1);
        await using var _ = fixture.Db;

        Assert.False(await fixture.Entitlements.CanUseFeature(fixture.CompanyId, PlanFeatureCodes.Payroll));
        Assert.False(await fixture.Entitlements.CanRunPayroll(fixture.CompanyId));
        Assert.True(await fixture.Entitlements.CanAddEmployee(fixture.CompanyId));
    }

    [Fact]
    public async Task Employee_limit_boundary_blocks_the_next_seat()
    {
        var under = await SeedAsync(code: "paid", payrollEnabled: true, limit: 18, activeEmployees: 17);
        await using var _ = under.Db;
        Assert.True((await under.Entitlements.GetActiveEmployeeUsage(under.CompanyId)).CanAdd);

        var atCap = await SeedAsync(code: "paid", payrollEnabled: true, limit: 18, activeEmployees: 18);
        await using var __ = atCap.Db;
        var usage = await atCap.Entitlements.GetActiveEmployeeUsage(atCap.CompanyId);
        Assert.Equal(18, usage.CurrentUsage);
        Assert.Equal(18, usage.MaximumAllowed);
        Assert.Equal(0, usage.Remaining);
        Assert.False(usage.CanAdd);
        Assert.False(await atCap.Entitlements.CanAddEmployee(atCap.CompanyId));
    }

    [Fact]
    public async Task Feature_enabled_and_disabled_follow_plan_rows_not_the_plan_name()
    {
        var fixture = await SeedAsync(code: "growth", payrollEnabled: true, limit: 10, activeEmployees: 0);
        await using var _ = fixture.Db;
        fixture.Db.PlanFeatures.Add(new PlanFeature
        {
            Id = Guid.NewGuid(),
            PlanId = fixture.PlanId,
            Code = PlanFeatureCodes.ApiAccess,
            IsEnabled = false
        });
        await fixture.Db.SaveChangesAsync();

        Assert.True(await fixture.Entitlements.CanUseFeature(fixture.CompanyId, PlanFeatureCodes.Payroll));
        Assert.False(await fixture.Entitlements.CanUseFeature(fixture.CompanyId, PlanFeatureCodes.ApiAccess));
        Assert.False(await fixture.Entitlements.CanUseFeature(fixture.CompanyId, PlanFeatureCodes.Loans));
    }

    [Fact]
    public async Task Tenant_isolation_hides_another_company_usage_and_features()
    {
        var companyA = await SeedAsync(code: "paid", payrollEnabled: true, limit: 10, activeEmployees: 4);
        var companyB = await SeedAsync(
            code: "free",
            payrollEnabled: false,
            limit: 2,
            activeEmployees: 2,
            database: companyA.Database);
        await using var _ = companyA.Db;

        var tenantA = new StaticTenantContext { UserId = Guid.NewGuid(), CompanyId = companyA.CompanyId };
        await using var db = TestDb.Create(tenantA, companyA.Database);
        var entitlements = new EntitlementService(db, tenantA);

        var own = await entitlements.GetActiveEmployeeUsage(companyA.CompanyId);
        Assert.Equal(4, own.CurrentUsage);
        Assert.True(await entitlements.CanUseFeature(companyA.CompanyId, PlanFeatureCodes.Payroll));

        var foreign = await entitlements.GetSnapshot(companyB.CompanyId);
        Assert.Equal(EntitlementSnapshot.None, foreign);
        Assert.False(await entitlements.CanUseFeature(companyB.CompanyId, PlanFeatureCodes.Payroll));
        Assert.False(await entitlements.CanAddEmployee(companyB.CompanyId));
    }

    [Theory]
    [InlineData(SubscriptionStatus.Expired, false, false, false)]
    [InlineData(SubscriptionStatus.Cancelled, false, false, false)]
    [InlineData(SubscriptionStatus.Trialing, true, false, false)]
    [InlineData(SubscriptionStatus.Suspended, true, false, false)]
    [InlineData(SubscriptionStatus.Active, true, true, true)]
    [InlineData(SubscriptionStatus.PastDue, true, true, true)]
    [InlineData(SubscriptionStatus.GracePeriod, true, true, true)]
    public async Task Status_matrix_matches_the_entitlement_plan(
        SubscriptionStatus status,
        bool canUsePayroll,
        bool canAdd,
        bool canRun)
    {
        var fixture = await SeedAsync(
            code: "paid",
            payrollEnabled: true,
            limit: 10,
            activeEmployees: 1,
            status: status);
        await using var _ = fixture.Db;

        Assert.Equal(canUsePayroll, await fixture.Entitlements.CanUseFeature(fixture.CompanyId, PlanFeatureCodes.Payroll));
        Assert.Equal(canAdd, await fixture.Entitlements.CanAddEmployee(fixture.CompanyId));
        Assert.Equal(canRun, await fixture.Entitlements.CanRunPayroll(fixture.CompanyId));
    }

    [Fact]
    public async Task Open_trial_can_add_employees_and_run_payroll()
    {
        var now = new DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);
        var fixture = await SeedAsync(
            code: "trial",
            payrollEnabled: true,
            limit: 10,
            activeEmployees: 1,
            status: SubscriptionStatus.Trialing,
            trialEndsAt: now.AddDays(7),
            time: new FrozenTimeProvider(now));
        await using var _ = fixture.Db;

        Assert.True(await fixture.Entitlements.CanAddEmployee(fixture.CompanyId));
        Assert.True(await fixture.Entitlements.CanRunPayroll(fixture.CompanyId));
        Assert.True((await fixture.Entitlements.GetSnapshot(fixture.CompanyId)).CanWrite);
    }

    [Fact]
    public void Evaluate_does_not_use_plan_names()
    {
        var features = new[]
        {
            new PlanFeature { Code = PlanFeatureCodes.Payroll, IsEnabled = true }
        };
        var starter = EntitlementRules.Evaluate(SubscriptionStatus.Active, features, 0, 5, 5);
        var enterprise = EntitlementRules.Evaluate(SubscriptionStatus.Active, features, 0, 5, 5);
        Assert.Equal(starter.CanRunPayroll, enterprise.CanRunPayroll);
        Assert.Equal(starter.Usage, enterprise.Usage);
        Assert.True(starter.CanRunPayroll);
    }

    private sealed record Fixture(
        string Database,
        MiniPayrollDbContext Db,
        EntitlementService Entitlements,
        Guid CompanyId,
        Guid PlanId);

    private static async Task<Fixture> SeedAsync(
        string code,
        bool payrollEnabled,
        int limit,
        int activeEmployees,
        SubscriptionStatus status = SubscriptionStatus.Active,
        string? database = null,
        DateTimeOffset? trialEndsAt = null,
        TimeProvider? time = null)
    {
        database ??= $"entitlement-{Guid.NewGuid():N}";
        var companyId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var plan = new Plan
        {
            Id = planId,
            Code = code,
            Name = code,
            IsActive = true,
            MaxActiveEmployees = limit,
            PricePerEmployee = 0m,
            DefaultEmployeeLimit = limit
        };
        plan.Features.Add(new PlanFeature
        {
            Id = Guid.NewGuid(),
            PlanId = planId,
            Code = PlanFeatureCodes.Payroll,
            IsEnabled = payrollEnabled
        });
        var company = new Company
        {
            Id = companyId,
            Name = code,
            ContactEmail = $"{companyId:N}@example.com",
            IsSetupComplete = true,
            SetupStep = CompanySetupStep.Complete,
            CreatedAt = DateTimeOffset.UtcNow,
            Subscription = new Subscription
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                PlanId = planId,
                Status = status,
                EmployeeLimit = limit,
                GracePeriodDays = 7,
                TrialEndsAt = trialEndsAt
            }
        };

        await using (var writer = TestDb.Create(NullTenantContext.Instance, database))
        {
            writer.Plans.Add(plan);
            writer.Companies.Add(company);
            for (var index = 0; index < activeEmployees; index++)
            {
                writer.Employees.Add(new Employee
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    EmployeeCode = $"E{index:00}",
                    FullName = $"Person {index}",
                    Phone = "9000000000",
                    Email = $"p{index}@example.com",
                    AddressLine1 = "Street",
                    City = "Pune",
                    State = "Maharashtra",
                    PostalCode = "411001",
                    Designation = "Staff",
                    Status = EmployeeStatus.Active,
                    CreatedAt = DateTimeOffset.UtcNow
                });
            }

            await writer.SaveChangesAsync();
        }

        var tenant = new StaticTenantContext { UserId = Guid.NewGuid(), CompanyId = companyId };
        var db = TestDb.Create(tenant, database);
        return new Fixture(
            database,
            db,
            new EntitlementService(db, tenant, time),
            companyId,
            planId);
    }

    private sealed class FrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
