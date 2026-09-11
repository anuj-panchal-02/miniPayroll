using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public sealed class PayrollInputServiceTests
{
    private const int Year = 2026;
    private const int Month = 8;

    [Fact]
    public async Task Get_period_returns_roster_without_a_run()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;

        var result = await fixture.Inputs.GetPeriodAsync(Year, Month);

        Assert.Equal(PayrollRunStatusCode.Success, result.Status);
        Assert.Null(result.Period!.Run);
        var employee = Assert.Single(result.Period.Employees);
        Assert.Equal(fixture.Employee.Id, employee.EmployeeId);
        Assert.True(employee.HasStructure);
        Assert.Null(employee.Attendance);
        Assert.Empty(result.Period.Results);
        Assert.Null(result.Period.Totals);
        Assert.Equal(26, result.Period.WorkingDaysPerMonth);
    }

    [Fact]
    public async Task Get_period_flags_missing_structure_and_skips_drafts()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        await SeedEmployeeAsync(fixture.Database, fixture.Company.Id, EmployeeStatus.Draft);
        var unstructured = await SeedEmployeeAsync(
            fixture.Database, fixture.Company.Id, EmployeeStatus.Active);

        var result = await fixture.Inputs.GetPeriodAsync(Year, Month);

        var ids = result.Period!.Employees.Select(item => item.EmployeeId).ToHashSet();
        Assert.Contains(fixture.Employee.Id, ids);
        Assert.Contains(unstructured.Id, ids);
        Assert.False(result.Period.Employees.Single(item => item.EmployeeId == unstructured.Id).HasStructure);
        Assert.DoesNotContain(
            result.Period.Employees,
            item => item.Status == EmployeeStatus.Draft);
    }

    [Fact]
    public async Task Invalid_periods_are_rejected()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;

        Assert.Equal(PayrollRunStatusCode.InvalidPeriod,
            (await fixture.Inputs.GetPeriodAsync(Year, 13)).Status);
    }

    [Fact]
    public async Task Bulk_save_round_trips_attendance_overtime_bonus_and_deduction()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var runId = (await fixture.Payroll.CreateRunAsync(Year, Month)).Run!.Id;

        var saved = await fixture.Inputs.SaveInputsAsync(runId, Payload(
            fixture.Employee.Id,
            working: 26, present: 24, paid: 1, unpaid: 1,
            overtimeHours: 10, overtimeRate: 150m,
            bonus: 2000m,
            deduction: 500m));

        Assert.Equal(PayrollRunStatusCode.Success, saved.Status);
        var employee = Assert.Single(saved.Period!.Employees);
        Assert.Equal(24m, employee.Attendance!.Present);
        Assert.Equal(1m, employee.Attendance.PaidLeave);
        Assert.Equal(10m, Assert.Single(employee.Overtime).Hours);
        Assert.Equal(150m, employee.Overtime[0].Rate);
        Assert.Equal(2000m, Assert.Single(employee.Bonuses).Amount);
        Assert.Equal(BonusType.Festival, employee.Bonuses[0].Type);
        Assert.Equal(500m, Assert.Single(employee.Deductions).Amount);
        Assert.Equal(OneTimeDeductionType.Tds, employee.Deductions[0].Type);

        Assert.Equal(1, await db.MonthlyAttendance.CountAsync());
        Assert.Equal(1, await db.AuditLogs.CountAsync(item => item.Action == AuditActions.PayrollInputsSave));
    }

    [Fact]
    public async Task Replace_all_removes_stale_one_time_rows()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var runId = (await fixture.Payroll.CreateRunAsync(Year, Month)).Run!.Id;

        await fixture.Inputs.SaveInputsAsync(runId, Payload(
            fixture.Employee.Id, overtimeHours: 8, overtimeRate: 100m, bonus: 500m));
        var second = await fixture.Inputs.SaveInputsAsync(runId, Payload(fixture.Employee.Id));

        Assert.Empty(second.Period!.Employees.Single().Overtime);
        Assert.Empty(second.Period.Employees.Single().Bonuses);
        Assert.Equal(0, await db.Overtime.CountAsync());
        Assert.Equal(0, await db.Bonuses.CountAsync());
        Assert.Equal(1, await db.MonthlyAttendance.CountAsync());
    }

    [Fact]
    public async Task Identity_violations_are_saved_and_surface_on_the_roster()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var runId = (await fixture.Payroll.CreateRunAsync(Year, Month)).Run!.Id;

        var saved = await fixture.Inputs.SaveInputsAsync(runId, Payload(
            fixture.Employee.Id, working: 26, present: 20, paid: 0, unpaid: 0));

        Assert.Equal(PayrollRunStatusCode.Success, saved.Status);
        Assert.Equal(20m, saved.Period!.Employees.Single().Attendance!.Present);
    }

    [Fact]
    public async Task Invalid_quantities_and_ineligible_employees_are_rejected()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var runId = (await fixture.Payroll.CreateRunAsync(Year, Month)).Run!.Id;
        var draft = await SeedEmployeeAsync(fixture.Database, fixture.Company.Id, EmployeeStatus.Draft);

        var badDays = await fixture.Inputs.SaveInputsAsync(runId, Payload(
            fixture.Employee.Id, working: 26.25m, present: 26.25m));
        var stranger = await fixture.Inputs.SaveInputsAsync(runId, Payload(Guid.NewGuid()));
        var draftEmployee = await fixture.Inputs.SaveInputsAsync(runId, Payload(draft.Id));
        var zeroBonus = await fixture.Inputs.SaveInputsAsync(
            runId,
            new PayrollInputsPayload(
                [new PayrollAttendanceInput(fixture.Employee.Id, 26, 26, 0, 0)],
                null,
                [new PayrollBonusInput(fixture.Employee.Id, BonusType.Festival, 0m, null)],
                null));

        Assert.Equal(PayrollRunStatusCode.InvalidInput, badDays.Status);
        Assert.Equal(PayrollRunStatusCode.InvalidInput, stranger.Status);
        Assert.Equal(PayrollRunStatusCode.InvalidInput, draftEmployee.Status);
        Assert.Equal(PayrollRunStatusCode.InvalidInput, zeroBonus.Status);
        Assert.Equal(0, await db.MonthlyAttendance.CountAsync());
    }

    [Fact]
    public async Task Saving_inputs_on_a_calculated_run_returns_it_to_draft_and_keeps_results()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var runId = (await fixture.Payroll.CreateRunAsync(Year, Month)).Run!.Id;
        await fixture.Inputs.SaveInputsAsync(runId, Payload(fixture.Employee.Id));
        var calculated = await fixture.Payroll.CalculateAsync(runId);
        Assert.Equal(PayrollRunStatus.Calculated, calculated.Run!.RunStatus);

        var saved = await fixture.Inputs.SaveInputsAsync(runId, Payload(
            fixture.Employee.Id, working: 26, present: 25, paid: 0, unpaid: 1));

        Assert.Equal(PayrollRunStatus.Draft, saved.Period!.Run!.Status);
        Assert.Null(saved.Period.Run.CalculatedAt);
        Assert.Single(saved.Period.Results);
        Assert.Equal(28000m, saved.Period.Results[0].NetSalary);
        Assert.Equal(28000m, saved.Period.Totals!.NetSalary);
        Assert.Equal(1, saved.Period.Totals.EmployeeCount);
    }

    [Fact]
    public async Task Finalized_runs_cannot_be_edited()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var runId = (await fixture.Payroll.CreateRunAsync(Year, Month)).Run!.Id;
        var run = await db.PayrollRuns.SingleAsync(item => item.Id == runId);
        run.Status = PayrollRunStatus.Finalized;
        await db.SaveChangesAsync();

        Assert.Equal(PayrollRunStatusCode.RunLocked,
            (await fixture.Inputs.SaveInputsAsync(runId, Payload(fixture.Employee.Id))).Status);
    }

    [Fact]
    public async Task Get_period_includes_totals_after_calculation()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var runId = (await fixture.Payroll.CreateRunAsync(Year, Month)).Run!.Id;
        await fixture.Inputs.SaveInputsAsync(runId, Payload(fixture.Employee.Id));
        await fixture.Payroll.CalculateAsync(runId);

        var period = await fixture.Inputs.GetPeriodAsync(Year, Month);

        Assert.Equal(PayrollRunStatus.Calculated, period.Period!.Run!.Status);
        Assert.Equal(28000m, period.Period.Totals!.GrossEarnings);
        Assert.Equal(0m, period.Period.Totals.TotalDeductions);
        Assert.Equal(28000m, period.Period.Totals.NetSalary);
        Assert.Equal(0, period.Period.Totals.ErrorCount);
        Assert.Equal(26m, period.Period.Employees.Single().Attendance!.WorkingDays);
    }

    [Fact]
    public async Task Tenant_cannot_read_or_edit_another_companys_run()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var runId = (await fixture.Payroll.CreateRunAsync(Year, Month)).Run!.Id;

        var other = await SeedCompanyAsync(fixture.Database);
        var otherDb = TestDb.Create(NewTenant(other.Id), fixture.Database);
        await using var owned = otherDb;
        var inputs = new PayrollInputService(otherDb, NewTenant(other.Id));

        Assert.Equal(PayrollRunStatusCode.NotFound,
            (await inputs.SaveInputsAsync(runId, Payload(fixture.Employee.Id))).Status);

        var foreignPeriod = await inputs.GetPeriodAsync(Year, Month);
        Assert.Null(foreignPeriod.Period!.Run);
        Assert.Empty(foreignPeriod.Period.Employees);
    }

    [Fact]
    public async Task Suspended_subscription_can_read_but_cannot_save()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var runId = (await fixture.Payroll.CreateRunAsync(Year, Month)).Run!.Id;
        var subscription = await db.Subscriptions.SingleAsync(
            item => item.CompanyId == fixture.Company.Id);
        subscription.Status = SubscriptionStatus.Suspended;
        await db.SaveChangesAsync();

        Assert.Equal(PayrollRunStatusCode.Success,
            (await fixture.Inputs.GetPeriodAsync(Year, Month)).Status);
        Assert.Equal(PayrollRunStatusCode.SubscriptionReadOnly,
            (await fixture.Inputs.SaveInputsAsync(runId, Payload(fixture.Employee.Id))).Status);
    }

    private static PayrollInputsPayload Payload(
        Guid employeeId,
        decimal working = 26,
        decimal present = 26,
        decimal paid = 0,
        decimal unpaid = 0,
        decimal? overtimeHours = null,
        decimal? overtimeRate = null,
        decimal? bonus = null,
        decimal? deduction = null) => new(
        [new PayrollAttendanceInput(employeeId, working, present, paid, unpaid)],
        overtimeHours is { } hours
            ? [new PayrollOvertimeInput(employeeId, hours, overtimeRate, null)]
            : [],
        bonus is { } bonusAmount
            ? [new PayrollBonusInput(employeeId, BonusType.Festival, bonusAmount, "Diwali")]
            : [],
        deduction is { } deductionAmount
            ? [new PayrollDeductionInput(employeeId, OneTimeDeductionType.Tds, deductionAmount, null)]
            : []);

    private sealed record Fixture(
        string Database,
        MiniPayrollDbContext Db,
        PayrollCalculationService Payroll,
        PayrollInputService Inputs,
        Company Company,
        Employee Employee);

    private static async Task<Fixture> FixtureAsync()
    {
        var database = $"payroll-inputs-{Guid.NewGuid():N}";
        var company = await SeedCompanyAsync(database);
        var employee = await SeedEmployeeAsync(database, company.Id, EmployeeStatus.Active);
        await SeedStructureAsync(database, company.Id, employee.Id);

        var tenant = NewTenant(company.Id);
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
            PfApplicable = false,
            EsiApplicable = false,
            WorkingDaysPerMonth = 26,
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
            OvertimeRate = 150m,
            Status = status,
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

    private static StaticTenantContext NewTenant(Guid companyId) => new()
    {
        UserId = Guid.NewGuid(),
        CompanyId = companyId,
        IsSuperadmin = false
    };
}
