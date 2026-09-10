using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public sealed class PayrollLifecycleTests
{
    private const int Year = 2026;
    private const int Month = 8;

    [Fact]
    public async Task Mark_paid_round_trips_and_refuses_draft()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var draftId = (await fixture.Payroll.CreateRunAsync(Year, Month)).Run!.Id;
        Assert.Equal(PayrollRunStatusCode.RunLocked,
            (await fixture.Inputs.SetPaymentAsync(draftId, fixture.Employee.Id, Paid())).Status);

        var runId = await FinalizedRunAsync(fixture, draftId);
        var saved = await fixture.Inputs.SetPaymentAsync(runId, fixture.Employee.Id, Paid());
        Assert.Equal(PayrollRunStatusCode.Success, saved.Status);
        var employee = Assert.Single(saved.Period!.Results);
        Assert.Equal(SalaryPaymentStatus.Paid, employee.PaymentStatus);
        Assert.Equal(SalaryPaymentMode.Bank, employee.PaymentMode);
        Assert.Equal(new DateOnly(2026, 9, 1), employee.PaidOn);
        Assert.Equal("TXN-1", employee.PaymentReference);
        Assert.Equal(28000m, employee.NetSalary);
        Assert.Equal(1, await db.AuditLogs.CountAsync(item => item.Action == AuditActions.PayrollPaymentUpdate));
    }

    [Fact]
    public async Task History_includes_totals_for_the_tenant_only()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var runId = await FinalizedRunAsync(fixture, (await fixture.Payroll.CreateRunAsync(Year, Month)).Run!.Id);

        var history = await fixture.Inputs.ListRunsAsync();
        var item = Assert.Single(history.Runs!);
        Assert.Equal(runId, item.Id);
        Assert.Equal(PayrollRunStatus.Finalized, item.Status);
        Assert.Equal(1, item.EmployeeCount);
        Assert.Equal(28000m, item.NetSalary);

        var other = await SeedCompanyAsync(fixture.Database);
        var otherTenant = new StaticTenantContext
        {
            UserId = Guid.NewGuid(),
            CompanyId = other.Id,
            IsSuperadmin = false
        };
        await using var otherDb = TestDb.Create(otherTenant, fixture.Database);
        var outsider = new PayrollInputService(otherDb, otherTenant);
        Assert.Empty((await outsider.ListRunsAsync()).Runs!);
        Assert.Equal(PayrollRunStatusCode.NotFound,
            (await outsider.SetPaymentAsync(runId, fixture.Employee.Id, Paid())).Status);
    }

    [Fact]
    public async Task Reversal_requires_superadmin_reason_and_allows_a_new_run()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var runId = await FinalizedRunAsync(fixture, (await fixture.Payroll.CreateRunAsync(Year, Month)).Run!.Id);

        Assert.Equal(PayrollRunStatusCode.Forbidden,
            (await fixture.Payroll.ReverseAsync(fixture.Company.Id, runId, "Mistake")).Status);

        var superTenant = new StaticTenantContext
        {
            UserId = Guid.NewGuid(),
            CompanyId = null,
            IsSuperadmin = true
        };
        await using var superDb = TestDb.Create(superTenant, fixture.Database);
        var super = new PayrollCalculationService(superDb, superTenant);
        Assert.Equal(PayrollRunStatusCode.InvalidInput,
            (await super.ReverseAsync(fixture.Company.Id, runId, "  ")).Status);

        var reversed = await super.ReverseAsync(fixture.Company.Id, runId, "Wrong attendance");
        Assert.Equal(PayrollRunStatusCode.Success, reversed.Status);
        Assert.Equal(PayrollRunStatus.Reversed, reversed.Run!.RunStatus);
        Assert.Equal(28000m, Assert.Single(reversed.Run.Employees).NetSalary);
        Assert.Equal(1, await db.PayrollEmployees.CountAsync(item => item.PayrollRunId == runId));
        Assert.Equal(1, await superDb.AuditLogs.CountAsync(item => item.Action == AuditActions.PayrollRunReverse));

        var replacement = await fixture.Payroll.CreateRunAsync(Year, Month);
        Assert.Equal(PayrollRunStatusCode.Success, replacement.Status);
        Assert.NotEqual(runId, replacement.Run!.Id);
    }

    private static PayrollPaymentPayload Paid() => new(
        SalaryPaymentStatus.Paid,
        SalaryPaymentMode.Bank,
        new DateOnly(2026, 9, 1),
        "TXN-1");

    private static async Task<Guid> FinalizedRunAsync(Fixture fixture, Guid runId)
    {
        fixture.Db.MonthlyAttendance.Add(new MonthlyAttendance
        {
            Id = Guid.NewGuid(),
            CompanyId = fixture.Company.Id,
            PayrollRunId = runId,
            EmployeeId = fixture.Employee.Id,
            WorkingDays = 26,
            Present = 26
        });
        await fixture.Db.SaveChangesAsync();
        Assert.Equal(PayrollRunStatusCode.Success, (await fixture.Payroll.CalculateAsync(runId)).Status);
        Assert.Equal(PayrollRunStatusCode.Success, (await fixture.Payroll.FinalizeAsync(runId)).Status);
        return runId;
    }

    private sealed record Fixture(
        string Database,
        MiniPayrollDbContext Db,
        PayrollCalculationService Payroll,
        PayrollInputService Inputs,
        Company Company,
        Employee Employee);

    private static async Task<Fixture> FixtureAsync()
    {
        var database = $"payroll-life-{Guid.NewGuid():N}";
        var company = await SeedCompanyAsync(database);
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

    private static async Task<Company> SeedCompanyAsync(string database)
    {
        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = "Acme",
            ContactEmail = $"{Guid.NewGuid():N}@example.com",
            IsSetupComplete = true,
            SetupStep = CompanySetupStep.Complete,
            DailyRateMethod = DailyRateMethod.CalendarDays,
            CreatedAt = DateTimeOffset.UtcNow
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
        structure.Components.Add(new EmployeeSalaryComponent
        {
            Id = Guid.NewGuid(),
            SalaryComponentId = Guid.NewGuid(),
            Name = "HRA",
            Type = SalaryComponentType.Earning,
            ValueType = SalaryComponentValueType.PercentageOfBasic,
            Value = 40m,
            SortOrder = 1
        });
        await using var db = TestDb.Create(NullTenantContext.Instance, database);
        db.SalaryStructures.Add(structure);
        await db.SaveChangesAsync();
    }
}
