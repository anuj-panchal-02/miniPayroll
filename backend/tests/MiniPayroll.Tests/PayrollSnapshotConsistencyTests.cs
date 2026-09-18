using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Payroll.Statutory;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Persistence;
using UglyToad.PdfPig;

namespace MiniPayroll.Tests;

public sealed class PayrollSnapshotConsistencyTests
{
    private const int Year = 2026;
    private const int Month = 8;

    [Fact]
    public async Task Calculate_persists_source_snapshot_amounts_and_rule_versions()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var runId = await CalculatedRunAsync(fixture);

        var run = await db.PayrollRuns.SingleAsync(item => item.Id == runId);
        Assert.Equal(PayrollRunStatus.Calculated, run.Status);
        Assert.False(string.IsNullOrEmpty(run.SourceFingerprint));
        Assert.False(string.IsNullOrEmpty(run.SourceSnapshotJson));
        Assert.Equal(new DateOnly(2014, 9, 1), run.PfRuleEffectiveFrom);
        Assert.Equal(new DateOnly(2019, 7, 1), run.EsiRuleEffectiveFrom);
        Assert.Contains("Basic Salary", run.SourceSnapshotJson);
        Assert.Contains("pfApplicable", run.SourceSnapshotJson);

        var row = await db.PayrollEmployees
            .Include(item => item.Earnings)
            .Include(item => item.Deductions)
            .SingleAsync(item => item.PayrollRunId == runId);
        Assert.Equal(28000m, row.NetSalary);
        Assert.Equal(1800m, row.EmployerPf);
        Assert.Equal(0m, Assert.Single(row.Deductions, line => line.StatutoryKind == StatutoryKind.PfEmployee).Amount);
        Assert.Equal(1800m, row.Deductions.Single(line => line.StatutoryKind == StatutoryKind.PfEmployee).ComputedAmount);
        Assert.Equal("Engineer", row.Designation);

        var period = await fixture.Inputs.GetPeriodAsync(Year, Month);
        Assert.False(period.Period!.Run!.SourceDrift);
    }

    [Fact]
    public async Task Employee_edit_after_calculate_blocks_finalize_and_keeps_amounts()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var runId = await CalculatedRunAsync(fixture);

        var employee = await db.Employees.SingleAsync(item => item.Id == fixture.Employee.Id);
        employee.Designation = "Lead Engineer";
        employee.PfCovered = false;
        employee.Gender = Gender.Male;
        await db.SaveChangesAsync();

        await AssertDriftAsync(fixture, runId, expectedNet: 28000m);
    }

    [Fact]
    public async Task Salary_edit_after_calculate_blocks_finalize()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var runId = await CalculatedRunAsync(fixture);

        var structure = await db.SalaryStructures.Include(item => item.Components)
            .SingleAsync(item => item.EmployeeId == fixture.Employee.Id);
        structure.Components.OrderBy(item => item.SortOrder).First().Value = 50000m;
        await db.SaveChangesAsync();

        await AssertDriftAsync(fixture, runId, expectedNet: 28000m);
    }

    [Fact]
    public async Task Statutory_policy_edit_after_calculate_blocks_finalize()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var runId = await CalculatedRunAsync(fixture);

        var company = await db.Companies.SingleAsync();
        company.PfApplicable = false;
        company.EsiApplicable = true;
        await db.SaveChangesAsync();

        await AssertDriftAsync(fixture, runId, expectedNet: 28000m);
    }

    [Fact]
    public async Task Roster_change_after_calculate_blocks_finalize()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var runId = await CalculatedRunAsync(fixture);

        await SeedEmployeeAsync(fixture.Database, fixture.Company.Id, "EMP-JOIN");

        await AssertDriftAsync(fixture, runId, expectedNet: 28000m);
    }

    [Fact]
    public async Task Recalculate_then_finalize_locks_the_new_snapshot_and_payslip()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var runId = await CalculatedRunAsync(fixture);

        var employee = await db.Employees.SingleAsync(item => item.Id == fixture.Employee.Id);
        employee.Designation = "Lead Engineer";
        var structure = await db.SalaryStructures.Include(item => item.Components)
            .SingleAsync(item => item.EmployeeId == fixture.Employee.Id);
        structure.Components.OrderBy(item => item.SortOrder).First().Value = 15000m;
        var company = await db.Companies.SingleAsync();
        company.PfUseWageCeiling = false;
        await db.SaveChangesAsync();

        Assert.Equal(PayrollRunStatusCode.SourceChanged, (await fixture.Payroll.FinalizeAsync(runId)).Status);

        var recalculated = await fixture.Payroll.CalculateAsync(runId);
        Assert.Equal(PayrollRunStatusCode.Success, recalculated.Status);
        var newNet = recalculated.Run!.Employees.Single().NetSalary;
        Assert.Equal(21000m, newNet); // 15000 + 40% HRA; PF override still 0
        Assert.Equal("Lead Engineer", recalculated.Run.Employees.Single().Designation);

        Assert.Equal(PayrollRunStatusCode.Success, (await fixture.Payroll.FinalizeAsync(runId)).Status);

        employee.Designation = "Changed After Finalize";
        structure.Components.OrderBy(item => item.SortOrder).First().Value = 80000m;
        company.PfApplicable = false;
        await db.SaveChangesAsync();

        var row = await db.PayrollEmployees
            .Include(item => item.Earnings)
            .Include(item => item.Deductions)
            .SingleAsync(item => item.PayrollRunId == runId);
        Assert.Equal(newNet, row.NetSalary);
        Assert.Equal("Lead Engineer", row.Designation);
        Assert.Equal(15000m, row.Earnings.OrderBy(line => line.SortOrder).First().Amount);

        var payslips = new PayrollPayslipService(db, fixture.Tenant, new PayslipPdfService());
        var file = (await payslips.GetEmployeeAsync(runId, fixture.Employee.Id, null)).File!;
        var text = PdfText(file.Content);
        Assert.Contains("Lead Engineer", text);
        Assert.DoesNotContain("Changed After Finalize", text);
        Assert.Equal(PayrollRunStatusCode.RunLocked, (await fixture.Payroll.CalculateAsync(runId)).Status);
        Assert.Equal(PayrollRunStatusCode.RunLocked, (await fixture.Payroll.FinalizeAsync(runId)).Status);
    }

    [Fact]
    public async Task Concurrent_run_edit_during_finalize_returns_conflict()
    {
        var fixture = await FixtureAsync();
        var runId = await CalculatedRunAsync(fixture);
        await using var db = fixture.Db;
        var run = await db.PayrollRuns.SingleAsync(item => item.Id == runId);
        run.RowVersion = [1];
        await db.SaveChangesAsync();

        var interceptor = new RacingRowVersionInterceptor(fixture.Database, runId);
        await using var racingDb = TestDb.Create(fixture.Tenant, fixture.Database, interceptor);
        var payroll = new PayrollCalculationService(racingDb, fixture.Tenant);

        var result = await payroll.FinalizeAsync(runId);

        Assert.True(interceptor.Raced);
        Assert.Equal(PayrollRunStatusCode.ConcurrencyConflict, result.Status);
        await using var verify = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        var persisted = await verify.PayrollRuns.SingleAsync(item => item.Id == runId);
        Assert.Equal(PayrollRunStatus.Calculated, persisted.Status);
    }

    [Fact]
    public async Task Saving_inputs_clears_the_source_fingerprint()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var runId = await CalculatedRunAsync(fixture);

        var saved = await fixture.Inputs.SaveInputsAsync(
            runId,
            new PayrollInputsPayload(
                [new PayrollAttendanceInput(fixture.Employee.Id, 26, 26, 0, 0)],
                [],
                [],
                []));

        Assert.Equal(PayrollRunStatus.Draft, saved.Period!.Run!.Status);
        Assert.False(saved.Period.Run.SourceDrift);
        var run = await db.PayrollRuns.SingleAsync(item => item.Id == runId);
        Assert.Null(run.SourceFingerprint);
        Assert.Null(run.SourceSnapshotJson);
        Assert.Equal(28000m, saved.Period.Results[0].NetSalary);
    }

    private static async Task AssertDriftAsync(Fixture fixture, Guid runId, decimal expectedNet)
    {
        var period = await fixture.Inputs.GetPeriodAsync(Year, Month);
        Assert.True(period.Period!.Run!.SourceDrift);
        Assert.Equal(expectedNet, period.Period.Results[0].NetSalary);

        Assert.Equal(PayrollRunStatusCode.SourceChanged, (await fixture.Payroll.FinalizeAsync(runId)).Status);

        var row = await fixture.Db.PayrollEmployees.SingleAsync(item => item.PayrollRunId == runId);
        Assert.Equal(expectedNet, row.NetSalary);
        Assert.Equal(PayrollRunStatus.Calculated, (await fixture.Db.PayrollRuns.SingleAsync(item => item.Id == runId)).Status);
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
        Assert.Equal(PayrollRunStatus.Calculated, calculated.Run!.RunStatus);

        var overridden = await fixture.Payroll.SetStatutoryOverridesAsync(
            runId,
            fixture.Employee.Id,
            [new StatutoryOverrideInput(StatutoryKind.PfEmployee, 0m)]);
        Assert.Equal(PayrollRunStatusCode.Success, overridden.Status);
        return runId;
    }

    private sealed record Fixture(
        MiniPayrollDbContext Db,
        PayrollCalculationService Payroll,
        PayrollInputService Inputs,
        Company Company,
        Employee Employee,
        StaticTenantContext Tenant,
        string Database);

    private static async Task<Fixture> FixtureAsync()
    {
        var database = $"payroll-snapshot-{Guid.NewGuid():N}";
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
            tenant,
            database);
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
            PfApplicable = true,
            PfUseWageCeiling = true,
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
        string? code = null)
    {
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            EmployeeCode = code ?? $"EMP-{Guid.NewGuid():N}",
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
            PfCovered = true,
            Gender = Gender.Female,
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
            SortOrder = 0,
            Kind = SalaryComponentKind.Basic
        });
        structure.Components.Add(new EmployeeSalaryComponent
        {
            Id = Guid.NewGuid(),
            SalaryComponentId = Guid.NewGuid(),
            Name = "HRA",
            Type = SalaryComponentType.Earning,
            ValueType = SalaryComponentValueType.PercentageOfBasic,
            Value = 40m,
            SortOrder = 1,
            Kind = SalaryComponentKind.Hra
        });

        await using var db = TestDb.Create(NullTenantContext.Instance, database);
        db.SalaryStructures.Add(structure);
        await db.SaveChangesAsync();
    }

    private static string PdfText(byte[] bytes)
    {
        using var document = PdfDocument.Open(bytes);
        return string.Join('\n', document.GetPages().Select(page => page.Text));
    }

    private sealed class RacingRowVersionInterceptor(string database, Guid runId) : SaveChangesInterceptor
    {
        public bool Raced { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Raced)
            {
                return result;
            }

            Raced = true;
            await using var racing = TestDb.Create(NullTenantContext.Instance, database);
            var persisted = await racing.PayrollRuns.SingleAsync(item => item.Id == runId, cancellationToken);
            persisted.RowVersion = [2];
            await racing.SaveChangesAsync(cancellationToken);
            return result;
        }
    }
}
