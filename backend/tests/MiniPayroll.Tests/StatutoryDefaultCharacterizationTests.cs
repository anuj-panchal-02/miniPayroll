using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Payroll;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

/// <summary>
/// Statutory coverage is off until payroll settings are saved. The original
/// AddStatutoryPayroll SQL default of true is left in that shipped file; a later
/// backfill turns existing flags off.
/// </summary>
public class StatutoryDefaultCharacterizationTests
{
    [Fact]
    public void New_company_clr_defaults_leave_pf_and_esi_off()
    {
        var company = new Company();

        Assert.False(company.PfApplicable);
        Assert.False(company.EsiApplicable);
        Assert.True(company.PfUseWageCeiling);
        Assert.Null(company.State);
    }

    [Fact]
    public void New_employee_clr_defaults_leave_coverage_off_with_unknown_gender()
    {
        var employee = new Employee();

        Assert.False(employee.PfCovered);
        Assert.False(employee.EsiCovered);
        Assert.Null(employee.Gender);
    }

    [Fact]
    public void AddStatutoryPayroll_migration_still_contains_the_original_true_sql_defaults()
    {
        var source = File.ReadAllText(MigrationPath("20260911092419_AddStatutoryPayroll.cs"));
        Assert.Matches(@"name: ""PfApplicable""[\s\S]*?defaultValue: true", source);
        Assert.Matches(@"name: ""EsiApplicable""[\s\S]*?defaultValue: true", source);
        Assert.Matches(@"name: ""PfCovered""[\s\S]*?defaultValue: true", source);
        Assert.Matches(@"name: ""EsiCovered""[\s\S]*?defaultValue: true", source);
        Assert.Matches(@"name: ""Gender""[\s\S]*?nullable: true", source);
    }

    [Fact]
    public void Backfill_migration_turns_existing_coverage_flags_off()
    {
        var source = File.ReadAllText(MigrationPath("20260911120000_DisableUnintendedStatutoryDefaults.cs"));
        Assert.Contains("UPDATE [mp_TblCompany] SET [PfApplicable] = 0, [EsiApplicable] = 0", source);
        Assert.Contains("UPDATE [mp_TblEmployee] SET [PfCovered] = 0, [EsiCovered] = 0", source);
        Assert.Matches(@"name: ""PfApplicable""[\s\S]*?defaultValue: false", source);
        Assert.Matches(@"name: ""EsiApplicable""[\s\S]*?defaultValue: false", source);
        Assert.Matches(@"name: ""PfCovered""[\s\S]*?defaultValue: false", source);
        Assert.Matches(@"name: ""EsiCovered""[\s\S]*?defaultValue: false", source);
    }

    [Fact]
    public async Task Company_left_on_clr_defaults_without_a_state_does_not_deduct_pf_or_esi()
    {
        var fixture = await FixtureAsync(state: null);
        await using var db = fixture.Db;
        var runId = await CalculatedRunAsync(fixture);
        var row = await db.PayrollEmployees
            .Include(item => item.Deductions)
            .SingleAsync(item => item.PayrollRunId == runId);

        Assert.Null(row.Errors);
        Assert.DoesNotContain(row.Deductions, line => line.Name == "Provident Fund (PF)");
        Assert.DoesNotContain(row.Deductions, line => line.Name == "ESI");
        Assert.DoesNotContain(row.Deductions, line => line.Name == "Professional Tax");
        Assert.DoesNotContain(row.Deductions, line => line.Name == "Labour Welfare Fund (LWF)");
        Assert.Equal(0m, row.EmployerPf);
        Assert.Equal(0m, row.EmployerEsi);
        Assert.Equal(28000m, row.NetSalary);
        Assert.Equal(row.GrossEarnings - row.TotalDeductions, row.NetSalary);
    }

    [Fact]
    public async Task Maharashtra_company_left_on_clr_defaults_blocks_when_gender_is_missing()
    {
        var fixture = await FixtureAsync(state: "Maharashtra");
        await using var db = fixture.Db;
        var runId = await CalculatedRunAsync(fixture);
        var row = await db.PayrollEmployees
            .Include(item => item.Deductions)
            .SingleAsync(item => item.PayrollRunId == runId);

        Assert.Contains(PayrollCalculationMessages.MissingGenderForProfessionalTax, row.Errors);
        Assert.Empty(row.Deductions);
        Assert.Equal(0m, row.EmployerPf);
        Assert.Equal(0m, row.EmployerEsi);
        Assert.Equal(0m, row.NetSalary);
    }

    [Fact]
    public async Task Opted_in_company_still_deducts_pf_and_esi()
    {
        var fixture = await FixtureAsync(state: null);
        await using var db = fixture.Db;
        var company = await db.Companies.SingleAsync();
        var employee = await db.Employees.SingleAsync();
        company.PfApplicable = true;
        company.EsiApplicable = true;
        employee.PfCovered = true;
        employee.EsiCovered = true;
        await db.SaveChangesAsync();

        var runId = await CalculatedRunAsync(fixture);
        var row = await db.PayrollEmployees
            .Include(item => item.Deductions)
            .SingleAsync(item => item.PayrollRunId == runId);

        Assert.Null(row.Errors);
        Assert.Equal(1800m, Assert.Single(row.Deductions, line => line.Name == "Provident Fund (PF)").Amount);
        Assert.Equal(210m, Assert.Single(row.Deductions, line => line.Name == "ESI").Amount);
        Assert.Equal(1800m, row.EmployerPf);
        Assert.Equal(910m, row.EmployerEsi);
        Assert.Equal(25990m, row.NetSalary);
        Assert.Equal(row.GrossEarnings - row.TotalDeductions, row.NetSalary);
    }

    private static string MigrationPath(string fileName)
    {
        var start = new DirectoryInfo(AppContext.BaseDirectory);
        while (start is not null)
        {
            var candidate = Path.Combine(
                start.FullName,
                "backend",
                "src",
                "MiniPayroll.Infrastructure",
                "Persistence",
                "Migrations",
                fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            start = start.Parent;
        }

        throw new FileNotFoundException($"Could not locate {fileName}.");
    }

    private static async Task<Guid> CalculatedRunAsync(Fixture fixture)
    {
        var runId = (await fixture.Payroll.CreateRunAsync(2026, 8)).Run!.Id;
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
        var calculated = await fixture.Payroll.CalculateAsync(runId);
        Assert.Equal(PayrollRunStatusCode.Success, calculated.Status);
        return runId;
    }

    private sealed record Fixture(
        MiniPayrollDbContext Db,
        PayrollCalculationService Payroll,
        Company Company,
        Employee Employee);

    private static async Task<Fixture> FixtureAsync(string? state)
    {
        var database = $"statutory-defaults-{Guid.NewGuid():N}";
        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = "Acme",
            ContactEmail = $"{Guid.NewGuid():N}@example.com",
            State = state,
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
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            EmployeeCode = "EMP-01",
            FullName = "Ada Lovelace",
            Phone = "9876543210",
            Email = "ada@example.com",
            AddressLine1 = "Main Road",
            City = "Pune",
            State = state ?? "Delhi",
            PostalCode = "411001",
            Designation = "Engineer",
            JoiningDate = new DateOnly(2026, 1, 1),
            BankName = "HDFC Bank",
            BankAccountNumber = "123456789012",
            Ifsc = "HDFC0001234",
            Status = EmployeeStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var structure = new SalaryStructure
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            EmployeeId = employee.Id,
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

        await using (var seed = TestDb.Create(NullTenantContext.Instance, database))
        {
            seed.Plans.Add(plan);
            seed.Companies.Add(company);
            seed.Employees.Add(employee);
            seed.SalaryStructures.Add(structure);
            await seed.SaveChangesAsync();
        }

        var tenant = new StaticTenantContext
        {
            UserId = Guid.NewGuid(),
            CompanyId = company.Id,
            IsSuperadmin = false
        };
        var db = TestDb.Create(tenant, database);
        return new Fixture(db, new PayrollCalculationService(db, tenant), company, employee);
    }
}
