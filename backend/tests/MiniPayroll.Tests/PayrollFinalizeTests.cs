using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public sealed class PayrollFinalizeTests
{
    private const int Year = 2026;
    private const int Month = 8;

    [Fact]
    public async Task Finalize_locks_calculated_run_and_snapshots_display_fields()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var runId = await CalculatedRunAsync(fixture);

        var result = await fixture.Payroll.FinalizeAsync(runId);

        Assert.Equal(PayrollRunStatusCode.Success, result.Status);
        Assert.Equal(PayrollRunStatus.Finalized, result.Run!.RunStatus);
        Assert.NotNull(result.Run.FinalizedAt);
        Assert.Equal("Engineer", Assert.Single(result.Run.Employees).Designation);

        var run = await db.PayrollRuns.SingleAsync(item => item.Id == runId);
        Assert.Equal(PayrollRunStatus.Finalized, run.Status);
        Assert.Equal("Acme", run.CompanyName);
        Assert.Equal(fixture.Tenant.UserId, run.FinalizedByUserId);
        Assert.Equal(1, await db.AuditLogs.CountAsync(item => item.Action == AuditActions.PayrollRunFinalize));
    }

    [Fact]
    public async Task Draft_run_cannot_be_finalized()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var runId = (await fixture.Payroll.CreateRunAsync(Year, Month)).Run!.Id;

        Assert.Equal(PayrollRunStatusCode.NotCalculated, (await fixture.Payroll.FinalizeAsync(runId)).Status);
    }

    [Fact]
    public async Task Double_finalize_is_locked()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var runId = await CalculatedRunAsync(fixture);

        Assert.Equal(PayrollRunStatusCode.Success, (await fixture.Payroll.FinalizeAsync(runId)).Status);
        Assert.Equal(PayrollRunStatusCode.RunLocked, (await fixture.Payroll.FinalizeAsync(runId)).Status);
    }

    [Fact]
    public async Task Salary_edit_after_finalize_leaves_snapshot_unchanged()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var runId = await CalculatedRunAsync(fixture);
        Assert.Equal(PayrollRunStatusCode.Success, (await fixture.Payroll.FinalizeAsync(runId)).Status);

        var structure = await db.SalaryStructures.Include(item => item.Components)
            .SingleAsync(item => item.EmployeeId == fixture.Employee.Id);
        structure.Components.OrderBy(item => item.SortOrder).First().Value = 50000m;
        await db.SaveChangesAsync();

        var row = await db.PayrollEmployees
            .Include(item => item.Earnings)
            .SingleAsync(item => item.PayrollRunId == runId);
        Assert.Equal(28000m, row.NetSalary);
        Assert.Equal(20000m, row.Earnings.OrderBy(item => item.SortOrder).First().Amount);
        Assert.Equal(PayrollRunStatusCode.RunLocked, (await fixture.Payroll.CalculateAsync(runId)).Status);
        Assert.Equal(PayrollRunStatusCode.RunLocked,
            (await fixture.Inputs.SaveInputsAsync(runId, new PayrollInputsPayload([], [], [], []))).Status);
    }

    private static async Task<Guid> CalculatedRunAsync(Fixture fixture)
    {
        var runId = (await fixture.Payroll.CreateRunAsync(Year, Month)).Run!.Id;
        fixture.Db.MonthlyAttendance.Add(new MonthlyAttendance
        {
            Id = Guid.NewGuid(),
            CompanyId = fixture.Company.Id,
            PayrollRunId = runId,
            EmployeeId = fixture.Employee.Id,
            WorkingDays = 26,
            Present = 26,
            PaidLeave = 0,
            UnpaidLeave = 0
        });
        await fixture.Db.SaveChangesAsync();
        var calculated = await fixture.Payroll.CalculateAsync(runId);
        Assert.Equal(PayrollRunStatusCode.Success, calculated.Status);
        return runId;
    }

    private sealed record Fixture(
        MiniPayrollDbContext Db,
        PayrollCalculationService Payroll,
        PayrollInputService Inputs,
        Company Company,
        Employee Employee,
        StaticTenantContext Tenant);

    private static async Task<Fixture> FixtureAsync()
    {
        var database = $"payroll-finalize-{Guid.NewGuid():N}";
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
            db,
            new PayrollCalculationService(db, tenant),
            new PayrollInputService(db, tenant),
            company,
            employee,
            tenant);
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
            PfApplicable = false,
            EsiApplicable = false,
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
