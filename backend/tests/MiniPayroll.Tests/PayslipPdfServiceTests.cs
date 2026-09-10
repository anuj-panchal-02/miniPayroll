using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public sealed class PayslipPdfServiceTests
{
    private const int Year = 2026;
    private const int Month = 8;

    [Fact]
    public async Task Finalized_run_renders_snapshot_amounts()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var runId = await FinalizedRunAsync(fixture);

        var pdf = new PayslipPdfService();
        var payslips = new PayrollPayslipService(db, fixture.Tenant, pdf);
        var single = await payslips.GetEmployeeAsync(runId, fixture.Employee.Id, null);
        var combined = await payslips.GetCombinedAsync(runId, null);

        Assert.Equal(PayrollRunStatusCode.Success, single.Status);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(single.File!.Content[..4]));
        Assert.True(single.File.Content.Length > 200);
        Assert.Equal(PayrollRunStatusCode.Success, combined.Status);
        Assert.True(combined.File!.Content.Length > 200);

        var run = await db.PayrollRuns.FindAsync(runId);
        var row = db.PayrollEmployees
            .Include(item => item.Earnings)
            .Include(item => item.Deductions)
            .Single(item => item.PayrollRunId == runId);
        var snapshot = PayslipPdfService.FromRun(run!, row, null);
        Assert.Equal(28000m, snapshot.NetSalary);
        Assert.Contains(snapshot.Earnings, line => line.Name == "Basic Salary" && line.Amount == 20000m);
        Assert.Contains("Twenty Eight thousand", MiniPayroll.Domain.Payroll.IndianRupeeWords.ToRupees(snapshot.NetSalary));
    }

    [Fact]
    public async Task Salary_change_does_not_change_payslip_snapshot()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var runId = await FinalizedRunAsync(fixture);
        var before = (await new PayrollPayslipService(db, fixture.Tenant, new PayslipPdfService())
            .GetEmployeeAsync(runId, fixture.Employee.Id, null)).File!.Content;

        var structure = db.SalaryStructures.Single();
        db.Entry(structure).Collection(item => item.Components).Load();
        structure.Components.OrderBy(item => item.SortOrder).First().Value = 90000m;
        await db.SaveChangesAsync();

        var after = (await new PayrollPayslipService(db, fixture.Tenant, new PayslipPdfService())
            .GetEmployeeAsync(runId, fixture.Employee.Id, null)).File!.Content;
        var row = db.PayrollEmployees.Single(item => item.PayrollRunId == runId);
        Assert.Equal(28000m, row.NetSalary);
        Assert.Equal(before.Length, after.Length);
    }

    [Fact]
    public async Task Draft_run_cannot_download_payslips()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var runId = (await fixture.Payroll.CreateRunAsync(Year, Month)).Run!.Id;
        var payslips = new PayrollPayslipService(db, fixture.Tenant, new PayslipPdfService());

        Assert.Equal(PayrollRunStatusCode.NotCalculated,
            (await payslips.GetCombinedAsync(runId, null)).Status);
    }

    private static async Task<Guid> FinalizedRunAsync(Fixture fixture)
    {
        var runId = (await fixture.Payroll.CreateRunAsync(Year, Month)).Run!.Id;
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
        MiniPayrollDbContext Db,
        PayrollCalculationService Payroll,
        Company Company,
        Employee Employee,
        StaticTenantContext Tenant);

    private static async Task<Fixture> FixtureAsync()
    {
        var database = $"payroll-payslip-{Guid.NewGuid():N}";
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
        return new Fixture(db, new PayrollCalculationService(db, tenant), company, employee, tenant);
    }
}
