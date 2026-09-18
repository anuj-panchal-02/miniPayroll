using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Billing;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Payroll;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public sealed class PriorPeriodPayrollHoldTests
{
    private static readonly DateTimeOffset Activated = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly PayrollPeriod August = new(2026, 8);
    private static readonly PayrollPeriod September = new(2026, 9);

    [Fact]
    public async Task First_month_can_run_and_the_next_month_is_held_until_paid()
    {
        var fixture = await FixtureAsync(Activated);
        await using var db = fixture.Db;

        Assert.Equal(PayrollRunStatusCode.Success, (await fixture.Payroll.CreateRunAsync(August.Year, August.Month)).Status);
        Assert.Equal(
            PayrollRunStatusCode.PriorPeriodUnpaid,
            (await fixture.Payroll.CreateRunAsync(September.Year, September.Month)).Status);

        var period = await fixture.Inputs.GetPeriodAsync(September.Year, September.Month);
        Assert.Equal(PayrollRunStatusCode.Success, period.Status);
        Assert.Equal("2026-08", period.Period!.BillingHoldPeriod);
    }

    [Fact]
    public async Task Superadmin_payment_unblocks_the_next_month()
    {
        var fixture = await FixtureAsync(Activated);
        await using var db = fixture.Db;

        Assert.Equal(
            PayrollRunStatusCode.PriorPeriodUnpaid,
            (await fixture.Payroll.CreateRunAsync(September.Year, September.Month)).Status);

        var super = new StaticTenantContext { IsSuperadmin = true, UserId = Guid.NewGuid() };
        await using var billingDb = TestDb.Create(super, fixture.Database);
        var billing = new BillingService(billingDb, super, new FrozenTimeProvider(Activated.AddDays(20)));
        Assert.Equal(
            BillingStatusCode.Success,
            (await billing.RecordPaymentAsync(
                fixture.Company.Id,
                new RecordPaymentRequest("2026-08", 49m, Activated, BillingCalculator.ModeUpi, null))).Status);

        Assert.Equal(
            PayrollRunStatusCode.Success,
            (await fixture.Payroll.CreateRunAsync(September.Year, September.Month)).Status);
    }

    [Fact]
    public async Task Paid_invoice_unblocks_without_relying_on_payroll_create()
    {
        var fixture = await FixtureAsync(Activated);
        await using var db = fixture.Db;

        db.Invoices.Add(new Invoice
        {
            Id = Guid.NewGuid(),
            CompanyId = fixture.Company.Id,
            SubscriptionId = fixture.Company.Subscription!.Id,
            PeriodStart = Activated,
            PeriodEnd = new DateTimeOffset(2026, 8, 31, 23, 59, 59, TimeSpan.Zero),
            Subtotal = 49m,
            Total = 49m,
            AmountPaid = 49m,
            Currency = "INR",
            Status = InvoiceStatus.Paid,
            PaidAt = Activated.AddDays(10),
            CreatedAt = Activated
        });
        await db.SaveChangesAsync();

        Assert.Equal(
            PayrollRunStatusCode.Success,
            (await fixture.Payroll.CreateRunAsync(September.Year, September.Month)).Status);
    }

    [Fact]
    public async Task Existing_draft_cannot_calculate_or_save_inputs_while_held()
    {
        var fixture = await FixtureAsync(new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero));
        await using var db = fixture.Db;

        var created = await fixture.Payroll.CreateRunAsync(September.Year, September.Month);
        Assert.Equal(PayrollRunStatusCode.Success, created.Status);

        var company = await db.Companies.SingleAsync(item => item.Id == fixture.Company.Id);
        company.ActivatedAt = Activated;
        await db.SaveChangesAsync();

        Assert.Equal(
            PayrollRunStatusCode.PriorPeriodUnpaid,
            (await fixture.Payroll.CalculateAsync(created.Run!.Id)).Status);
        Assert.Equal(
            PayrollRunStatusCode.PriorPeriodUnpaid,
            (await fixture.Payroll.FinalizeAsync(created.Run.Id)).Status);
        Assert.Equal(
            PayrollRunStatusCode.PriorPeriodUnpaid,
            (await fixture.Payroll.SetStatutoryOverridesAsync(
                created.Run.Id,
                fixture.Employee.Id,
                [])).Status);
        Assert.Equal(
            PayrollRunStatusCode.PriorPeriodUnpaid,
            (await fixture.Inputs.SaveInputsAsync(created.Run.Id, new PayrollInputsPayload([], [], [], []))).Status);

        var period = await fixture.Inputs.GetPeriodAsync(September.Year, September.Month);
        Assert.Equal(PayrollRunStatusCode.Success, period.Status);
        Assert.Equal("2026-08", period.Period!.BillingHoldPeriod);
        Assert.Equal(created.Run.Id, period.Period.Run!.Id);
    }

    [Fact]
    public async Task Salary_payment_on_the_prior_finalized_run_still_works()
    {
        var fixture = await FixtureAsync(Activated);
        await using var db = fixture.Db;

        var augustId = (await fixture.Payroll.CreateRunAsync(August.Year, August.Month)).Run!.Id;
        db.MonthlyAttendance.Add(new MonthlyAttendance
        {
            Id = Guid.NewGuid(),
            CompanyId = fixture.Company.Id,
            PayrollRunId = augustId,
            EmployeeId = fixture.Employee.Id,
            WorkingDays = 26,
            Present = 26
        });
        await db.SaveChangesAsync();
        Assert.Equal(PayrollRunStatusCode.Success, (await fixture.Payroll.CalculateAsync(augustId)).Status);
        Assert.Equal(PayrollRunStatusCode.Success, (await fixture.Payroll.FinalizeAsync(augustId)).Status);

        Assert.Equal(
            PayrollRunStatusCode.PriorPeriodUnpaid,
            (await fixture.Payroll.CreateRunAsync(September.Year, September.Month)).Status);

        var paid = await fixture.Inputs.SetPaymentAsync(
            augustId,
            fixture.Employee.Id,
            new PayrollPaymentPayload(
                SalaryPaymentStatus.Paid,
                SalaryPaymentMode.Bank,
                new DateOnly(2026, 9, 1),
                "TXN-1"));
        Assert.Equal(PayrollRunStatusCode.Success, paid.Status);
        Assert.Equal(SalaryPaymentStatus.Paid, Assert.Single(paid.Period!.Results).PaymentStatus);
    }

    [Fact]
    public async Task Suspended_still_returns_subscription_read_only()
    {
        var fixture = await FixtureAsync(Activated);
        await using var db = fixture.Db;

        var subscription = await db.Subscriptions.SingleAsync(
            item => item.CompanyId == fixture.Company.Id);
        subscription.Status = SubscriptionStatus.Suspended;
        await db.SaveChangesAsync();

        Assert.Equal(
            PayrollRunStatusCode.SubscriptionReadOnly,
            (await fixture.Payroll.CreateRunAsync(September.Year, September.Month)).Status);
    }

    private sealed record Fixture(
        string Database,
        MiniPayrollDbContext Db,
        PayrollCalculationService Payroll,
        PayrollInputService Inputs,
        Company Company,
        Employee Employee);

    private static async Task<Fixture> FixtureAsync(DateTimeOffset activatedAt)
    {
        var database = $"payroll-hold-{Guid.NewGuid():N}";
        var company = await SeedCompanyAsync(database, activatedAt);
        var employee = await SeedEmployeeAsync(database, company.Id);
        await SeedStructureAsync(database, company.Id, employee.Id);
        var tenant = new StaticTenantContext
        {
            UserId = Guid.NewGuid(),
            CompanyId = company.Id,
            IsSuperadmin = false
        };
        var db = TestDb.Create(tenant, database);
        return new Fixture(
            database,
            db,
            new PayrollCalculationService(db, tenant),
            new PayrollInputService(db, tenant),
            company,
            employee);
    }

    private static async Task<Company> SeedCompanyAsync(string database, DateTimeOffset activatedAt)
    {
        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = "Acme",
            ContactEmail = $"{Guid.NewGuid():N}@example.com",
            IsSetupComplete = true,
            SetupStep = CompanySetupStep.Complete,
            DailyRateMethod = DailyRateMethod.CalendarDays,
            PfApplicable = false,
            EsiApplicable = false,
            WorkingDaysPerMonth = 26,
            CreatedAt = activatedAt,
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

    private static async Task<Employee> SeedEmployeeAsync(string database, Guid companyId)
    {
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            EmployeeCode = $"EMP-{Guid.NewGuid():N}",
            FullName = "Ada Lovelace",
            Phone = "9876543210",
            Email = "ada@example.com",
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
            CreatedAt = DateTimeOffset.UtcNow
        };
        await using var db = TestDb.Create(NullTenantContext.Instance, database);
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        return employee;
    }

    private static async Task SeedStructureAsync(string database, Guid companyId, Guid employeeId)
    {
        var structure = new SalaryStructure
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            EmployeeId = employeeId,
            EffectiveFrom = new DateOnly(2026, 1, 1),
            CreatedAt = DateTimeOffset.UtcNow
        };
        structure.Components.Add(new EmployeeSalaryComponent
        {
            Id = Guid.NewGuid(),
            SalaryComponentId = Guid.NewGuid(),
            Name = "Basic Salary",
            Type = SalaryComponentType.Earning,
            ValueType = SalaryComponentValueType.FixedAmount,
            Value = 20000m,
            SortOrder = 0
        });
        await using var db = TestDb.Create(NullTenantContext.Instance, database);
        db.SalaryStructures.Add(structure);
        await db.SaveChangesAsync();
    }

    private sealed class FrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
