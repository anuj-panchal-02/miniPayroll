using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Payroll;
using MiniPayroll.Domain.Payroll.Statutory;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

/// <summary>
/// Maharashtra professional tax requires gender. Missing gender blocks calculate
/// instead of charging ₹0 or defaulting Gender to Male.
/// </summary>
public class MaharashtraNullGenderPtTests
{
    private static readonly PayrollPeriod August = new(2026, 8);
    private static readonly DateOnly LongAgo = new(2025, 1, 1);

    private static readonly IReadOnlyList<PayrollStructureLine> BasicPlusHra =
    [
        new("Basic Salary", SalaryComponentType.Earning, SalaryComponentValueType.FixedAmount, 20000m, 0),
        new("HRA", SalaryComponentType.Earning, SalaryComponentValueType.PercentageOfBasic, 40m, 1),
    ];

    [Fact]
    public void Maharashtra_slab_lookup_returns_zero_when_gender_is_null()
    {
        Assert.Equal(0m, ProfessionalTaxCalculator.Calculate("Maharashtra", gender: null, 28000m, 8));
        Assert.Equal(200m, ProfessionalTaxCalculator.Calculate("Maharashtra", Gender.Male, 28000m, 8));
        Assert.Equal(200m, ProfessionalTaxCalculator.Calculate("Maharashtra", Gender.Female, 28000m, 8));
    }

    [Fact]
    public void Payroll_calculation_blocks_and_omits_pt_when_maharashtra_gender_is_null()
    {
        var result = PayrollCalculator.Calculate(Input(gender: null));

        Assert.True(result.HasBlockingErrors);
        Assert.Contains(PayrollCalculationMessages.MissingGenderForProfessionalTax, result.Errors);
        Assert.Empty(result.Lines);
        Assert.DoesNotContain(result.Lines, line => line.StatutoryKind == StatutoryKind.ProfessionalTax);
        Assert.Equal(0m, result.NetSalary);
    }

    [Fact]
    public void Payroll_calculation_charges_two_hundred_pt_when_maharashtra_gender_is_male()
    {
        var result = PayrollCalculator.Calculate(Input(gender: Gender.Male));

        Assert.False(result.HasBlockingErrors);
        Assert.Equal(200m, Assert.Single(result.Lines, line => line.StatutoryKind == StatutoryKind.ProfessionalTax).Amount);
        Assert.Equal(27775m, result.NetSalary);
        PayrollMoney.AssertLineTotals(result);
    }

    [Fact]
    public void Empty_statutory_policy_does_not_require_gender()
    {
        var result = PayrollCalculator.Calculate(new PayrollEmployeeInput(
            August,
            DailyRateMethod.CalendarDays,
            LongAgo,
            null,
            new PayrollAttendance(26, 26, 0, 0),
            LongAgo,
            BasicPlusHra));

        Assert.False(result.HasBlockingErrors);
        Assert.Empty(result.Errors);
        Assert.Equal(28000m, result.NetSalary);
    }

    [Fact]
    public async Task Calculated_run_for_active_maharashtra_employee_with_null_gender_is_blocked()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
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
        Assert.Equal(PayrollRunStatus.Draft, calculated.Run!.RunStatus);

        var employee = Assert.Single(calculated.Run.Employees);
        Assert.Null(fixture.Employee.Gender);
        Assert.Contains(PayrollCalculationMessages.MissingGenderForProfessionalTax, employee.Errors);
        Assert.Empty(employee.Deductions);
        Assert.DoesNotContain(employee.Deductions, line => line.StatutoryKind == StatutoryKind.ProfessionalTax);
        Assert.Equal(0m, employee.NetSalary);
    }

    private static PayrollEmployeeInput Input(Gender? gender) => new(
        August,
        DailyRateMethod.CalendarDays,
        LongAgo,
        null,
        new PayrollAttendance(26, 26, 0, 0),
        LongAgo,
        BasicPlusHra,
        Statutory: new StatutoryPolicy(
            PfApplicable: false,
            PfUseWageCeiling: true,
            EsiApplicable: false,
            CompanyState: "Maharashtra",
            PfCovered: false,
            EsiCovered: false,
            Gender: gender));

    private sealed record Fixture(
        MiniPayrollDbContext Db,
        PayrollCalculationService Payroll,
        Company Company,
        Employee Employee);

    private static async Task<Fixture> FixtureAsync()
    {
        var database = $"mh-null-gender-{Guid.NewGuid():N}";
        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = "Acme",
            ContactEmail = $"{Guid.NewGuid():N}@example.com",
            State = "Maharashtra",
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
            State = "Maharashtra",
            PostalCode = "411001",
            Designation = "Engineer",
            JoiningDate = new DateOnly(2026, 1, 1),
            BankName = "HDFC Bank",
            BankAccountNumber = "123456789012",
            Ifsc = "HDFC0001234",
            Status = EmployeeStatus.Active,
            Gender = null,
            PfCovered = false,
            EsiCovered = false,
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
