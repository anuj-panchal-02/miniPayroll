using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Billing;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public sealed class BillingServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Clock = new FrozenTimeProvider(Now);

    [Fact]
    public async Task Get_is_forbidden_for_company_admins()
    {
        var fixture = await FixtureAsync();
        var companyAdmin = new StaticTenantContext
        {
            UserId = Guid.NewGuid(),
            CompanyId = fixture.Company.Id,
            IsSuperadmin = false
        };
        await using var db = TestDb.Create(companyAdmin, fixture.Database);
        var billing = new BillingService(db, companyAdmin, Clock);

        Assert.Equal(BillingStatusCode.Forbidden, (await billing.GetAsync(fixture.Company.Id)).Status);
    }

    [Fact]
    public async Task Get_returns_empty_periods_until_the_company_is_activated()
    {
        var fixture = await FixtureAsync(activated: false);
        var result = await fixture.Billing.GetAsync(fixture.Company.Id);

        Assert.Equal(BillingStatusCode.Success, result.Status);
        Assert.Equal(49m, result.Billing!.PricePerEmployee);
        Assert.Equal(7, result.Billing.GracePeriodDays);
        Assert.Empty(result.Billing.Periods);
    }

    [Fact]
    public async Task Get_uses_finalized_employees_even_after_later_deactivation()
    {
        var fixture = await FixtureAsync();
        var second = await SeedEmployeeAsync(fixture.Database, fixture.Company.Id, "Second");
        await SeedFinalizedRunAsync(fixture, 2026, 8, [fixture.Employee.Id, second.Id]);

        await using (var db = TestDb.Create(NullTenantContext.Instance, fixture.Database))
        {
            var employee = await db.Employees.SingleAsync(item => item.Id == second.Id);
            employee.Status = EmployeeStatus.Inactive;
            employee.ExitDate = new DateOnly(2026, 8, 25);
            await db.SaveChangesAsync();
        }

        var result = await fixture.Billing.GetAsync(fixture.Company.Id);
        var period = Assert.Single(result.Billing!.Periods);
        Assert.Equal("2026-08", period.BillingPeriod);
        Assert.Equal(2, period.BillableEmployees);
        Assert.Equal(BillableSource.FinalizedPayroll, period.BillableSource);
        Assert.Equal(98m, period.AmountDue);
        Assert.False(period.Prorated);
        Assert.Equal(0m, period.PaidAmount);
        Assert.Equal(98m, period.Remaining);
    }

    [Fact]
    public async Task Get_falls_back_to_eligibility_after_reversal()
    {
        var fixture = await FixtureAsync();
        var second = await SeedEmployeeAsync(fixture.Database, fixture.Company.Id, "Second");
        var runId = await SeedFinalizedRunAsync(fixture, 2026, 8, [fixture.Employee.Id, second.Id]);

        await using (var db = TestDb.Create(NullTenantContext.Instance, fixture.Database))
        {
            var run = await db.PayrollRuns.SingleAsync(item => item.Id == runId);
            run.Status = PayrollRunStatus.Reversed;
            var employee = await db.Employees.SingleAsync(item => item.Id == second.Id);
            employee.Status = EmployeeStatus.Inactive;
            employee.ExitDate = new DateOnly(2026, 7, 31);
            await db.SaveChangesAsync();
        }

        var period = Assert.Single((await fixture.Billing.GetAsync(fixture.Company.Id)).Billing!.Periods);
        Assert.Equal(1, period.BillableEmployees);
        Assert.Equal(BillableSource.ActiveHeadcount, period.BillableSource);
        Assert.Equal(49m, period.AmountDue);
    }

    [Fact]
    public async Task Get_prorates_the_activation_month()
    {
        var fixture = await FixtureAsync(activatedAt: new DateTimeOffset(2026, 8, 10, 8, 0, 0, TimeSpan.Zero));

        var period = Assert.Single((await fixture.Billing.GetAsync(fixture.Company.Id)).Billing!.Periods);
        Assert.Equal(1, period.BillableEmployees);
        Assert.Equal(BillableSource.ActiveHeadcount, period.BillableSource);
        Assert.True(period.Prorated);
        Assert.Equal(34.77m, period.AmountDue);
    }

    [Fact]
    public async Task Record_payment_is_forbidden_for_company_admins()
    {
        var fixture = await FixtureAsync();
        var companyAdmin = new StaticTenantContext
        {
            UserId = Guid.NewGuid(),
            CompanyId = fixture.Company.Id,
            IsSuperadmin = false
        };
        await using var db = TestDb.Create(companyAdmin, fixture.Database);
        var billing = new BillingService(db, companyAdmin, Clock);

        Assert.Equal(
            BillingStatusCode.Forbidden,
            (await billing.RecordPaymentAsync(fixture.Company.Id, ValidPayment())).Status);
    }

    [Fact]
    public async Task Record_payment_writes_the_row_and_audit_without_changing_status()
    {
        var fixture = await FixtureAsync();
        var result = await fixture.Billing.RecordPaymentAsync(fixture.Company.Id, ValidPayment("GST-88"));

        Assert.Equal(BillingStatusCode.Success, result.Status);
        var period = Assert.Single(result.Billing!.Periods);
        var payment = Assert.Single(period.Payments);
        Assert.Equal(40m, payment.Amount);
        Assert.Equal("UPI", payment.PaymentMode);
        Assert.Equal("GST-88", payment.InvoiceGstReference);
        Assert.Equal(40m, period.PaidAmount);
        Assert.Equal(9m, period.Remaining);

        await using var db = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        Assert.Equal(1, await db.AuditLogs.CountAsync(item => item.Action == AuditActions.BillingPaymentRecord));
        var subscription = await db.Subscriptions.SingleAsync(item => item.CompanyId == fixture.Company.Id);
        Assert.Equal(SubscriptionStatus.Active, subscription.Status);
    }

    [Fact]
    public async Task Record_payment_allows_multiple_partial_collections()
    {
        var fixture = await FixtureAsync();
        Assert.Equal(BillingStatusCode.Success,
            (await fixture.Billing.RecordPaymentAsync(fixture.Company.Id, ValidPayment(amount: 20m))).Status);
        var result = await fixture.Billing.RecordPaymentAsync(
            fixture.Company.Id,
            ValidPayment(amount: 10m, mode: BillingCalculator.ModeNeft));

        var period = Assert.Single(result.Billing!.Periods);
        Assert.Equal(2, period.Payments.Count);
        Assert.Equal(30m, period.PaidAmount);
        Assert.Equal(19m, period.Remaining);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10.001)]
    public async Task Record_payment_rejects_invalid_amounts(decimal amount)
    {
        var fixture = await FixtureAsync();
        Assert.Equal(
            BillingStatusCode.InvalidInput,
            (await fixture.Billing.RecordPaymentAsync(fixture.Company.Id, ValidPayment(amount: amount))).Status);
    }

    [Fact]
    public async Task Record_payment_rejects_unknown_modes_and_out_of_range_periods()
    {
        var fixture = await FixtureAsync();
        Assert.Equal(
            BillingStatusCode.InvalidInput,
            (await fixture.Billing.RecordPaymentAsync(
                fixture.Company.Id,
                ValidPayment() with { PaymentMode = "Bank" })).Status);
        Assert.Equal(
            BillingStatusCode.InvalidPeriod,
            (await fixture.Billing.RecordPaymentAsync(
                fixture.Company.Id,
                ValidPayment() with { BillingPeriod = "2026-07" })).Status);
    }

    [Fact]
    public async Task Record_payment_rejects_an_unactivated_company()
    {
        var fixture = await FixtureAsync(activated: false);
        Assert.Equal(
            BillingStatusCode.NotActivated,
            (await fixture.Billing.RecordPaymentAsync(fixture.Company.Id, ValidPayment())).Status);
    }

    [Fact]
    public async Task Closed_month_snapshot_ignores_a_later_price_change()
    {
        var fixture = await FixtureAsync();
        var september = new FrozenTimeProvider(new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero));
        await using var db = TestDb.Create(
            new StaticTenantContext { UserId = Guid.NewGuid(), IsSuperadmin = true },
            fixture.Database);
        var billing = new BillingService(db, new StaticTenantContext { IsSuperadmin = true }, september);

        var first = Assert.Single(
            (await billing.GetAsync(fixture.Company.Id)).Billing!.Periods,
            period => period.BillingPeriod == "2026-08");
        Assert.False(first.IsEstimated);
        Assert.Equal(49m, first.PricePerEmployee);
        Assert.Equal(49m, first.AmountDue);

        await using (var mutate = TestDb.Create(NullTenantContext.Instance, fixture.Database))
        {
            var plan = await mutate.Plans.SingleAsync(item => item.Id == fixture.Company.Subscription!.PlanId);
            plan.PricePerEmployee = 79m;
            await mutate.SaveChangesAsync();
        }

        var again = Assert.Single(
            (await billing.GetAsync(fixture.Company.Id)).Billing!.Periods,
            period => period.BillingPeriod == "2026-08");
        Assert.Equal(49m, again.PricePerEmployee);
        Assert.Equal(49m, again.AmountDue);
        Assert.Equal(1, await db.BillingPeriods.CountAsync(item => item.BillingPeriod == "2026-08"));
    }

    [Fact]
    public async Task Open_month_follows_the_live_plan_price()
    {
        var fixture = await FixtureAsync();
        var first = Assert.Single((await fixture.Billing.GetAsync(fixture.Company.Id)).Billing!.Periods);
        Assert.True(first.IsEstimated);
        Assert.Equal(49m, first.PricePerEmployee);

        await using (var mutate = TestDb.Create(NullTenantContext.Instance, fixture.Database))
        {
            var plan = await mutate.Plans.SingleAsync(item => item.Id == fixture.Company.Subscription!.PlanId);
            plan.PricePerEmployee = 79m;
            await mutate.SaveChangesAsync();
        }

        await using var db = TestDb.Create(
            new StaticTenantContext { IsSuperadmin = true },
            fixture.Database);
        var billing = new BillingService(db, new StaticTenantContext { IsSuperadmin = true }, Clock);
        var again = Assert.Single((await billing.GetAsync(fixture.Company.Id)).Billing!.Periods);
        Assert.True(again.IsEstimated);
        Assert.Equal(79m, again.PricePerEmployee);
        Assert.Equal(79m, again.AmountDue);
    }

    [Fact]
    public async Task Company_admin_can_read_own_billing_but_cannot_record_payment()
    {
        var fixture = await FixtureAsync();
        var companyAdmin = new StaticTenantContext
        {
            UserId = Guid.NewGuid(),
            CompanyId = fixture.Company.Id,
            IsSuperadmin = false
        };
        await using var db = TestDb.Create(companyAdmin, fixture.Database);
        var billing = new BillingService(db, companyAdmin, Clock);

        var own = await billing.GetOwnAsync();
        Assert.Equal(BillingStatusCode.Success, own.Status);
        Assert.Single(own.Billing!.Periods);
        Assert.Equal(
            BillingStatusCode.Forbidden,
            (await billing.RecordPaymentAsync(fixture.Company.Id, ValidPayment())).Status);
    }

    [Fact]
    public async Task Superadmin_cannot_read_own_company_billing()
    {
        var fixture = await FixtureAsync();
        Assert.Equal(BillingStatusCode.Forbidden, (await fixture.Billing.GetOwnAsync()).Status);
    }

    [Fact]
    public async Task Billing_extends_through_later_finalized_months()
    {
        var fixture = await FixtureAsync();
        await SeedFinalizedRunAsync(fixture, 2026, 11, [fixture.Employee.Id]);
        await SeedFinalizedRunAsync(fixture, 2026, 12, [fixture.Employee.Id]);
        var september = new FrozenTimeProvider(new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero));
        await using var db = TestDb.Create(
            new StaticTenantContext { UserId = Guid.NewGuid(), IsSuperadmin = true },
            fixture.Database);
        var billing = new BillingService(db, new StaticTenantContext { IsSuperadmin = true }, september);

        var result = await billing.GetAsync(fixture.Company.Id);
        Assert.Equal(
            new[] { "2026-08", "2026-09", "2026-10", "2026-11", "2026-12" },
            result.Billing!.Periods.Select(period => period.BillingPeriod));

        var august = Assert.Single(result.Billing.Periods, period => period.BillingPeriod == "2026-08");
        Assert.False(august.IsEstimated);
        Assert.Equal(BillableSource.ActiveHeadcount, august.BillableSource);

        var septemberPeriod = Assert.Single(result.Billing.Periods, period => period.BillingPeriod == "2026-09");
        Assert.True(septemberPeriod.IsEstimated);

        var october = Assert.Single(result.Billing.Periods, period => period.BillingPeriod == "2026-10");
        Assert.False(october.IsEstimated);
        Assert.Equal(BillableSource.ActiveHeadcount, october.BillableSource);

        var december = Assert.Single(result.Billing.Periods, period => period.BillingPeriod == "2026-12");
        Assert.False(december.IsEstimated);
        Assert.Equal(BillableSource.FinalizedPayroll, december.BillableSource);
        Assert.Equal(1, december.BillableEmployees);

        Assert.Equal(
            BillingStatusCode.Success,
            (await billing.RecordPaymentAsync(
                fixture.Company.Id,
                ValidPayment() with { BillingPeriod = "2026-12", Amount = 49m })).Status);
        Assert.Equal(
            BillingStatusCode.InvalidPeriod,
            (await billing.RecordPaymentAsync(
                fixture.Company.Id,
                ValidPayment() with { BillingPeriod = "2026-07" })).Status);

        await using (var mutate = TestDb.Create(NullTenantContext.Instance, fixture.Database))
        {
            var plan = await mutate.Plans.SingleAsync(item => item.Id == fixture.Company.Subscription!.PlanId);
            plan.PricePerEmployee = 79m;
            await mutate.SaveChangesAsync();
        }

        await using var priced = TestDb.Create(
            new StaticTenantContext { IsSuperadmin = true },
            fixture.Database);
        var afterPrice = new BillingService(priced, new StaticTenantContext { IsSuperadmin = true }, september);
        var again = (await afterPrice.GetAsync(fixture.Company.Id)).Billing!;
        Assert.Equal(49m, Assert.Single(again.Periods, period => period.BillingPeriod == "2026-08").PricePerEmployee);
        Assert.Equal(79m, Assert.Single(again.Periods, period => period.BillingPeriod == "2026-09").PricePerEmployee);
        Assert.Equal(79m, Assert.Single(again.Periods, period => period.BillingPeriod == "2026-12").PricePerEmployee);
    }

    private static RecordPaymentRequest ValidPayment(
        string? gst = null,
        decimal amount = 40m,
        string mode = BillingCalculator.ModeUpi) =>
        new("2026-08", amount, Now, mode, gst);

    private sealed record Fixture(
        string Database,
        BillingService Billing,
        Company Company,
        Employee Employee);

    private static async Task<Fixture> FixtureAsync(
        bool activated = true,
        DateTimeOffset? activatedAt = null)
    {
        var database = $"billing-{Guid.NewGuid():N}";
        var activation = activatedAt ?? new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        var company = await SeedCompanyAsync(database, activated ? activation : null);
        var employee = await SeedEmployeeAsync(database, company.Id, "Ada");
        var tenant = new StaticTenantContext
        {
            UserId = Guid.NewGuid(),
            CompanyId = null,
            IsSuperadmin = true
        };
        var db = TestDb.Create(tenant, database);
        return new Fixture(database, new BillingService(db, tenant, Clock), company, employee);
    }

    private static async Task<Company> SeedCompanyAsync(string database, DateTimeOffset? activatedAt)
    {
        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = "Acme",
            ContactEmail = $"{Guid.NewGuid():N}@example.com",
            IsSetupComplete = true,
            SetupStep = CompanySetupStep.Complete,
            CreatedAt = new DateTimeOffset(2026, 7, 1, 0, 0, 0, TimeSpan.Zero),
            ActivatedAt = activatedAt
        };
        var plan = new Plan
        {
            Id = Guid.NewGuid(),
            Name = $"Basic-{Guid.NewGuid():N}",
            PricePerEmployee = 49m,
            DefaultEmployeeLimit = 9
        };
        company.Subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
            EmployeeLimit = 9,
            GracePeriodDays = 7
        };
        await using var db = TestDb.Create(NullTenantContext.Instance, database);
        db.Plans.Add(plan);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return company;
    }

    private static async Task<Employee> SeedEmployeeAsync(string database, Guid companyId, string name)
    {
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            EmployeeCode = $"EMP-{Guid.NewGuid():N}"[..12],
            FullName = name,
            Phone = "9876543210",
            Email = $"{Guid.NewGuid():N}@example.com",
            AddressLine1 = "Main Road",
            City = "Pune",
            State = "Maharashtra",
            PostalCode = "411001",
            Designation = "Engineer",
            JoiningDate = new DateOnly(2026, 1, 1),
            BankName = "HDFC Bank",
            BankAccountNumber = "123456789012",
            Ifsc = "HDFC0001234",
            Status = EmployeeStatus.Active,
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)
        };
        await using var db = TestDb.Create(NullTenantContext.Instance, database);
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        return employee;
    }

    private static async Task<Guid> SeedFinalizedRunAsync(
        Fixture fixture,
        int year,
        int month,
        IReadOnlyList<Guid> employeeIds)
    {
        var runId = Guid.NewGuid();
        await using var db = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        db.PayrollRuns.Add(new PayrollRun
        {
            Id = runId,
            CompanyId = fixture.Company.Id,
            Year = year,
            Month = month,
            Status = PayrollRunStatus.Finalized,
            DailyRateMethod = DailyRateMethod.CalendarDays,
            CreatedAt = Now,
            FinalizedAt = Now
        });
        foreach (var employeeId in employeeIds)
        {
            db.PayrollEmployees.Add(new PayrollEmployee
            {
                Id = Guid.NewGuid(),
                CompanyId = fixture.Company.Id,
                PayrollRunId = runId,
                EmployeeId = employeeId,
                EmployeeCode = "E",
                FullName = "N",
                Designation = "D",
                DaysEmployed = 31,
                DailyRate = 1000m,
                GrossEarnings = 31000m,
                NetSalary = 31000m
            });
        }

        await db.SaveChangesAsync();
        return runId;
    }

    private sealed class FrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
