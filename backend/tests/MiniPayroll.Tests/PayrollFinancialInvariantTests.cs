using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Payroll;
using MiniPayroll.Domain.Payroll.Statutory;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public class PayrollFinancialInvariantTests
{
    private static readonly PayrollPeriod August = new(2026, 8);
    private static readonly DateOnly LongAgo = new(2025, 1, 1);

    private static readonly IReadOnlyList<PayrollStructureLine> BasicPlusHra =
    [
        new("Basic Salary", SalaryComponentType.Earning, SalaryComponentValueType.FixedAmount, 20000m, 0),
        new("HRA", SalaryComponentType.Earning, SalaryComponentValueType.PercentageOfBasic, 40m, 1),
    ];

    [Fact]
    public void Gross_equals_sum_of_earning_lines_and_net_equals_gross_minus_deduction_lines()
    {
        var result = PayrollCalculator.Calculate(new PayrollEmployeeInput(
            August,
            DailyRateMethod.CalendarDays,
            LongAgo,
            null,
            new PayrollAttendance(26, 25, 0, 1),
            LongAgo,
            BasicPlusHra,
            [new PayrollOvertimeEntry(8m, 125m)],
            [new PayrollAmountEntry("Festival Bonus", 1500m)],
            [new PayrollAmountEntry("TDS", 400m)],
            Statutory: new StatutoryPolicy(true, true, true, "Maharashtra", true, true, Gender.Male)));

        PayrollMoney.AssertLineTotals(result);
        Assert.Equal(
            result.Lines.Where(line => line.Type == SalaryComponentType.Earning).Sum(line => line.Amount),
            result.GrossEarnings);
        Assert.Equal(
            result.Lines.Where(line => line.Type == SalaryComponentType.Deduction).Sum(line => line.Amount),
            result.TotalDeductions);
        Assert.Equal(result.GrossEarnings - result.TotalDeductions, result.NetSalary);
    }

    [Fact]
    public void Employer_pf_and_esi_do_not_reduce_employee_net()
    {
        var result = PayrollCalculator.Calculate(new PayrollEmployeeInput(
            August,
            DailyRateMethod.CalendarDays,
            LongAgo,
            null,
            new PayrollAttendance(26, 26, 0, 0),
            LongAgo,
            BasicPlusHra,
            Statutory: new StatutoryPolicy(true, true, true, "Delhi", true, true, Gender.Male)));

        Assert.Equal(1800m, result.EmployerPf);
        Assert.Equal(910m, result.EmployerEsi);
        Assert.DoesNotContain(result.Lines, line => line.Name.Contains("Employer", StringComparison.Ordinal));
        var employeePf = Assert.Single(result.Lines, line => line.StatutoryKind == StatutoryKind.PfEmployee).Amount;
        var employeeEsi = Assert.Single(result.Lines, line => line.StatutoryKind == StatutoryKind.EsiEmployee).Amount;
        Assert.Equal(28000m - employeePf - employeeEsi, result.NetSalary);
        Assert.Equal(25990m, result.NetSalary);
        PayrollMoney.AssertLineTotals(result);
    }

    [Fact]
    public void Zero_net_is_currently_a_warning_not_a_blocking_error()
    {
        var result = PayrollCalculator.Calculate(new PayrollEmployeeInput(
            August,
            DailyRateMethod.CalendarDays,
            LongAgo,
            null,
            new PayrollAttendance(26, 26, 0, 0),
            LongAgo,
            BasicPlusHra,
            OneTimeDeductions: [new PayrollAmountEntry("Loan installment", 28000m)]));

        Assert.False(result.HasBlockingErrors);
        Assert.Equal(0m, result.NetSalary);
        Assert.Contains(PayrollCalculationMessages.ZeroNet, result.Warnings);
        PayrollMoney.AssertLineTotals(result);
    }

    [Fact]
    public void Negative_net_is_currently_a_blocking_error_and_still_persists_the_negative_amount()
    {
        var result = PayrollCalculator.Calculate(new PayrollEmployeeInput(
            August,
            DailyRateMethod.CalendarDays,
            LongAgo,
            null,
            new PayrollAttendance(26, 26, 0, 0),
            LongAgo,
            BasicPlusHra,
            OneTimeDeductions: [new PayrollAmountEntry("Loan installment", 30000m)]));

        Assert.True(result.HasBlockingErrors);
        Assert.Contains(PayrollCalculationMessages.NegativeNet, result.Errors);
        Assert.Equal(-2000m, result.NetSalary);
        PayrollMoney.AssertLineTotals(result);
    }

    [Fact]
    public async Task Negative_net_currently_blocks_finalization_by_keeping_the_run_in_draft()
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
        fixture.Db.Deductions.Add(new Deduction
        {
            Id = Guid.NewGuid(),
            CompanyId = fixture.Company.Id,
            PayrollRunId = runId,
            EmployeeId = fixture.Employee.Id,
            Type = OneTimeDeductionType.LoanInstallment,
            Amount = 30000m
        });
        await fixture.Db.SaveChangesAsync();

        var calculated = await fixture.Payroll.CalculateAsync(runId);
        Assert.Equal(PayrollRunStatusCode.Success, calculated.Status);
        Assert.Equal(PayrollRunStatus.Draft, calculated.Run!.RunStatus);
        Assert.Equal(-2000m, Assert.Single(calculated.Run.Employees).NetSalary);
        Assert.Contains(PayrollCalculationMessages.NegativeNet, Assert.Single(calculated.Run.Employees).Errors);

        Assert.Equal(PayrollRunStatusCode.NotCalculated, (await fixture.Payroll.FinalizeAsync(runId)).Status);
        Assert.Equal(PayrollRunStatus.Draft, (await db.PayrollRuns.SingleAsync(item => item.Id == runId)).Status);
    }

    private sealed record Fixture(
        MiniPayrollDbContext Db,
        PayrollCalculationService Payroll,
        Company Company,
        Employee Employee);

    private static async Task<Fixture> FixtureAsync()
    {
        var database = $"payroll-invariants-{Guid.NewGuid():N}";
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
