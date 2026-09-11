using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Payroll;
using MiniPayroll.Domain.Payroll.Statutory;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public sealed class PayrollCalculationServiceTests
{
    // August 2026 has 31 calendar days.
    private const int Year = 2026;
    private const int Month = 8;

    [Fact]
    public async Task Create_run_rejects_duplicate_periods_until_reversed()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;

        var first = await fixture.Payroll.CreateRunAsync(Year, Month);
        Assert.Equal(PayrollRunStatusCode.Success, first.Status);
        Assert.Equal(PayrollRunStatus.Draft, first.Run!.RunStatus);
        Assert.Equal(DailyRateMethod.CalendarDays, first.Run.DailyRateMethod);

        var duplicate = await fixture.Payroll.CreateRunAsync(Year, Month);
        Assert.Equal(PayrollRunStatusCode.DuplicateRun, duplicate.Status);

        var run = await db.PayrollRuns.SingleAsync(item => item.Id == first.Run.Id);
        run.Status = PayrollRunStatus.Reversed;
        await db.SaveChangesAsync();

        var replacement = await fixture.Payroll.CreateRunAsync(Year, Month);
        Assert.Equal(PayrollRunStatusCode.Success, replacement.Status);
    }

    [Fact]
    public async Task Invalid_periods_are_rejected()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;

        Assert.Equal(PayrollRunStatusCode.InvalidPeriod,
            (await fixture.Payroll.CreateRunAsync(Year, 13)).Status);
        Assert.Equal(PayrollRunStatusCode.InvalidPeriod,
            (await fixture.Payroll.CalculatePeriodAsync(1999, 1)).Status);
    }

    [Fact]
    public async Task Calculate_persists_snapshot_lines_and_marks_run_calculated()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var runId = (await fixture.Payroll.CreateRunAsync(Year, Month)).Run!.Id;
        await AddAttendanceAsync(db, fixture.Company.Id, runId, fixture.Employee.Id);

        var result = await fixture.Payroll.CalculateAsync(runId);

        Assert.Equal(PayrollRunStatusCode.Success, result.Status);
        Assert.Equal(PayrollRunStatus.Calculated, result.Run!.RunStatus);
        Assert.NotNull(result.Run.CalculatedAt);

        var detail = Assert.Single(result.Run.Employees);
        Assert.Equal(28000m, detail.NetSalary); // Basic 20000 + HRA 40% = 8000
        Assert.Empty(detail.Errors);
        Assert.Equal(2, detail.Earnings.Count);

        var row = await db.PayrollEmployees.SingleAsync(item => item.PayrollRunId == runId);
        Assert.Equal(28000m, row.NetSalary);
        Assert.Null(row.Errors);
        Assert.Equal(2, await db.PayrollEarnings.CountAsync(line => line.PayrollEmployeeId == row.Id));
    }

    [Fact]
    public async Task Missing_attendance_keeps_run_in_draft_with_error_row()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var runId = (await fixture.Payroll.CreateRunAsync(Year, Month)).Run!.Id;

        var result = await fixture.Payroll.CalculateAsync(runId);

        Assert.Equal(PayrollRunStatusCode.Success, result.Status);
        Assert.Equal(PayrollRunStatus.Draft, result.Run!.RunStatus);
        var detail = Assert.Single(result.Run.Employees);
        Assert.Contains(PayrollCalculationService.MissingAttendanceError, detail.Errors);
    }

    [Fact]
    public async Task Recalculation_replaces_previous_snapshot_rows()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var runId = (await fixture.Payroll.CreateRunAsync(Year, Month)).Run!.Id;
        await AddAttendanceAsync(db, fixture.Company.Id, runId, fixture.Employee.Id);

        var first = await fixture.Payroll.CalculateAsync(runId);
        Assert.Equal(28000m, first.Run!.Employees.Single().NetSalary);

        var attendance = await db.MonthlyAttendance.SingleAsync(item => item.PayrollRunId == runId);
        attendance.Present = 25;
        attendance.UnpaidLeave = 1;
        await db.SaveChangesAsync();

        var second = await fixture.Payroll.CalculateAsync(runId);

        // 28000 / 31 ≈ 903.23 → 903 deducted for one unpaid day.
        Assert.Equal(27097m, second.Run!.Employees.Single().NetSalary);
        Assert.Equal(1, await db.PayrollEmployees.CountAsync(row => row.PayrollRunId == runId));
        var row = await db.PayrollEmployees.SingleAsync(item => item.PayrollRunId == runId);
        Assert.Equal(2, await db.PayrollEarnings.CountAsync(line => line.PayrollEmployeeId == row.Id));
        Assert.Equal(1, await db.PayrollDeductions.CountAsync(line => line.PayrollEmployeeId == row.Id));
    }

    [Fact]
    public async Task Draft_employees_are_skipped_and_inactive_employees_paid_only_in_exit_month()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;

        var draft = await SeedEmployeeAsync(fixture.Database, fixture.Company.Id, EmployeeStatus.Draft);
        var exitsThisMonth = await SeedEmployeeAsync(
            fixture.Database, fixture.Company.Id, EmployeeStatus.Inactive, exitDate: new DateOnly(2026, 8, 10));
        var exitedLastMonth = await SeedEmployeeAsync(
            fixture.Database, fixture.Company.Id, EmployeeStatus.Inactive, exitDate: new DateOnly(2026, 7, 31));
        await SeedStructureAsync(fixture.Database, fixture.Company.Id, exitsThisMonth.Id, basic: 31000m);

        var runId = (await fixture.Payroll.CreateRunAsync(Year, Month)).Run!.Id;
        await AddAttendanceAsync(db, fixture.Company.Id, runId, fixture.Employee.Id);
        await AddAttendanceAsync(db, fixture.Company.Id, runId, exitsThisMonth.Id,
            working: 8, present: 8);

        var result = await fixture.Payroll.CalculateAsync(runId);

        var employeeIds = result.Run!.Employees.Select(item => item.EmployeeId).ToHashSet();
        Assert.Contains(fixture.Employee.Id, employeeIds);
        Assert.Contains(exitsThisMonth.Id, employeeIds);
        Assert.DoesNotContain(draft.Id, employeeIds);
        Assert.DoesNotContain(exitedLastMonth.Id, employeeIds);

        var exited = result.Run.Employees.Single(item => item.EmployeeId == exitsThisMonth.Id);
        Assert.Equal(10, exited.DaysEmployed);
        Assert.Equal(10000m, exited.GrossEarnings); // 31000 × 10/31
        Assert.Contains(PayrollCalculationMessages.Prorated, exited.Warnings);
    }

    [Fact]
    public async Task Finalized_and_reversed_runs_cannot_be_recalculated()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var runId = (await fixture.Payroll.CreateRunAsync(Year, Month)).Run!.Id;

        var run = await db.PayrollRuns.SingleAsync(item => item.Id == runId);
        run.Status = PayrollRunStatus.Finalized;
        await db.SaveChangesAsync();

        Assert.Equal(PayrollRunStatusCode.RunLocked,
            (await fixture.Payroll.CalculateAsync(runId)).Status);
    }

    [Fact]
    public async Task Suspended_subscription_blocks_payroll()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;

        var subscription = await db.Subscriptions.SingleAsync(
            item => item.CompanyId == fixture.Company.Id);
        subscription.Status = SubscriptionStatus.Suspended;
        await db.SaveChangesAsync();

        Assert.Equal(PayrollRunStatusCode.SubscriptionReadOnly,
            (await fixture.Payroll.CreateRunAsync(Year, Month)).Status);
    }

    [Fact]
    public async Task Superadmin_tenant_cannot_run_company_payroll()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;

        var superadminDb = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        await using var owned = superadminDb;
        var payroll = new PayrollCalculationService(superadminDb, NullTenantContext.Instance);

        Assert.Equal(PayrollRunStatusCode.CompanyNotFound,
            (await payroll.CreateRunAsync(Year, Month)).Status);
    }

    [Fact]
    public async Task Calculate_period_creates_the_run_when_missing_and_reuses_it_afterwards()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;

        var first = await fixture.Payroll.CalculatePeriodAsync(Year, Month);
        Assert.Equal(PayrollRunStatusCode.Success, first.Status);
        Assert.Equal(PayrollRunStatus.Draft, first.Run!.RunStatus); // no attendance yet

        await AddAttendanceAsync(db, fixture.Company.Id, first.Run.Id, fixture.Employee.Id);
        var second = await fixture.Payroll.CalculatePeriodAsync(Year, Month);

        Assert.Equal(first.Run.Id, second.Run!.Id);
        Assert.Equal(PayrollRunStatus.Calculated, second.Run.RunStatus);
        Assert.Equal(1, await db.PayrollRuns.CountAsync());
    }

    [Fact]
    public async Task Statutory_override_survives_recalculation()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var company = await db.Companies.SingleAsync();
        company.PfApplicable = true;
        company.PfUseWageCeiling = true;
        await db.SaveChangesAsync();

        var runId = (await fixture.Payroll.CreateRunAsync(Year, Month)).Run!.Id;
        await AddAttendanceAsync(db, fixture.Company.Id, runId, fixture.Employee.Id);

        var calculated = await fixture.Payroll.CalculateAsync(runId);
        var pf = Assert.Single(
            calculated.Run!.Employees.Single().Deductions,
            line => line.StatutoryKind == StatutoryKind.PfEmployee);
        Assert.Equal(1800m, pf.Amount);
        Assert.Equal(1800m, calculated.Run.Employees.Single().EmployerPf);

        var overridden = await fixture.Payroll.SetStatutoryOverridesAsync(
            runId,
            fixture.Employee.Id,
            [new StatutoryOverrideInput(StatutoryKind.PfEmployee, 0m)]);
        Assert.Equal(PayrollRunStatusCode.Success, overridden.Status);
        pf = Assert.Single(
            overridden.Run!.Employees.Single().Deductions,
            line => line.StatutoryKind == StatutoryKind.PfEmployee);
        Assert.Equal(0m, pf.Amount);
        Assert.Equal(1800m, pf.ComputedAmount);

        var again = await fixture.Payroll.CalculateAsync(runId);
        pf = Assert.Single(
            again.Run!.Employees.Single().Deductions,
            line => line.StatutoryKind == StatutoryKind.PfEmployee);
        Assert.Equal(0m, pf.Amount);
        Assert.Equal(1800m, pf.ComputedAmount);
        Assert.Equal(1800m, again.Run.Employees.Single().EmployerPf);
    }

    private sealed record Fixture(
        string Database,
        MiniPayrollDbContext Db,
        PayrollCalculationService Payroll,
        Company Company,
        Employee Employee);

    private static async Task<Fixture> FixtureAsync()
    {
        var database = $"payroll-runs-{Guid.NewGuid():N}";
        var company = await SeedCompanyAsync(database);
        var employee = await SeedEmployeeAsync(database, company.Id, EmployeeStatus.Active);
        await SeedStructureAsync(database, company.Id, employee.Id, basic: 20000m, hraPercent: 40m);

        var tenant = NewTenant(company.Id);
        var db = TestDb.Create(tenant, database);
        return new Fixture(database, db, new PayrollCalculationService(db, tenant), company, employee);
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

    private static async Task<Employee> SeedEmployeeAsync(
        string database,
        Guid companyId,
        EmployeeStatus status,
        DateOnly? exitDate = null)
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
            ExitDate = exitDate,
            BankName = "HDFC Bank",
            BankAccountNumber = "123456789012",
            Ifsc = "HDFC0001234",
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await using var db = TestDb.Create(NullTenantContext.Instance, database);
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        return employee;
    }

    private static async Task SeedStructureAsync(
        string database,
        Guid companyId,
        Guid employeeId,
        decimal basic,
        decimal? hraPercent = null)
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
            Value = basic,
            SortOrder = 0
        });
        if (hraPercent is { } percent)
        {
            structure.Components.Add(new EmployeeSalaryComponent
            {
                Id = Guid.NewGuid(),
                SalaryComponentId = Guid.NewGuid(),
                Name = "HRA",
                Type = SalaryComponentType.Earning,
                ValueType = SalaryComponentValueType.PercentageOfBasic,
                Value = percent,
                SortOrder = 1
            });
        }

        await using var db = TestDb.Create(NullTenantContext.Instance, database);
        db.SalaryStructures.Add(structure);
        await db.SaveChangesAsync();
    }

    private static async Task AddAttendanceAsync(
        MiniPayrollDbContext db,
        Guid companyId,
        Guid runId,
        Guid employeeId,
        decimal working = 26,
        decimal? present = null,
        decimal paidLeave = 0,
        decimal unpaidLeave = 0)
    {
        db.MonthlyAttendance.Add(new MonthlyAttendance
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            PayrollRunId = runId,
            EmployeeId = employeeId,
            WorkingDays = working,
            Present = present ?? working - paidLeave - unpaidLeave,
            PaidLeave = paidLeave,
            UnpaidLeave = unpaidLeave
        });
        await db.SaveChangesAsync();
    }

    private static StaticTenantContext NewTenant(Guid companyId) => new()
    {
        UserId = Guid.NewGuid(),
        CompanyId = companyId,
        IsSuperadmin = false
    };
}
