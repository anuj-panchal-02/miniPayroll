using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Persistence;
using UglyToad.PdfPig;

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
        Assert.Contains(PayslipPdfService.BrandWatermark, PdfText(single.File.Content));
        Assert.DoesNotContain("Employer PF", PdfText(single.File.Content));
        Assert.Equal(31, snapshot.DaysEmployed);
        Assert.Equal(0m, snapshot.EmployerPf);
        Assert.Equal(0m, snapshot.EmployerEsi);
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
        var run = await db.PayrollRuns.FindAsync(runId);

        Assert.Equal("1 Main Street, New Delhi, Delhi, 110001", run!.CompanyAddress);
        Assert.Equal("MH/123", run.PfEstablishmentCode);
        Assert.Equal("ESI-1", run.EsiCode);
        Assert.Equal(PayrollRunStatusCode.NotCalculated,
            (await payslips.GetCombinedAsync(runId, null)).Status);
    }

    [Fact]
    public void FromRun_maps_company_address_codes_days_and_employer_costs()
    {
        var run = new PayrollRun
        {
            CompanyName = "Acme",
            CompanyAddress = "1 Main Street, Pune, Maharashtra, 411001",
            PfEstablishmentCode = "MH/123",
            EsiCode = "ESI-1",
            Year = Year,
            Month = Month,
            Status = PayrollRunStatus.Finalized
        };
        var row = new PayrollEmployee
        {
            FullName = "Ada Lovelace",
            EmployeeCode = "EMP-01",
            Designation = "Engineer",
            DaysEmployed = 31,
            GrossEarnings = 28000m,
            TotalDeductions = 1800m,
            NetSalary = 26200m,
            EmployerPf = 1800m,
            EmployerEsi = 585m
        };

        var snapshot = PayslipPdfService.FromRun(run, row, null);

        Assert.Equal("1 Main Street, Pune, Maharashtra, 411001", snapshot.CompanyAddress);
        Assert.Equal("MH/123", snapshot.PfEstablishmentCode);
        Assert.Equal("ESI-1", snapshot.EsiCode);
        Assert.Equal(31, snapshot.DaysEmployed);
        Assert.Equal(1800m, snapshot.EmployerPf);
        Assert.Equal(585m, snapshot.EmployerEsi);
        Assert.False(snapshot.Reversed);
    }

    [Fact]
    public void Render_embeds_minipayroll_watermark_on_every_slip()
    {
        var bytes = new PayslipPdfService().RenderCombined(
        [
            SampleSlip(reversed: false),
            SampleSlip(reversed: false) with { EmployeeName = "Grace Hopper", EmployeeCode = "EMP-02" }
        ]);
        using var document = PdfDocument.Open(bytes);

        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(bytes[..4]));
        Assert.Equal(2, document.NumberOfPages);
        foreach (var page in document.GetPages())
        {
            Assert.Contains(PayslipPdfService.BrandWatermark, PageText(page));
            Assert.DoesNotContain(PayslipPdfService.ReversedWatermark, PageText(page));
        }

        var text = PdfText(bytes);
        Assert.Contains("Employer PF", text);
        Assert.Contains("This is a computer-generated payslip.", text);
    }

    [Fact]
    public void Render_adds_reversed_overlay_on_top_of_brand_watermark()
    {
        var text = PdfText(new PayslipPdfService().Render(SampleSlip(reversed: true)));

        Assert.Contains(PayslipPdfService.BrandWatermark, text);
        Assert.Contains(PayslipPdfService.ReversedWatermark, text);
    }

    [Fact]
    public void Render_omits_employer_section_when_costs_are_zero()
    {
        var text = PdfText(new PayslipPdfService().Render(SampleSlip(employerPf: 0m, employerEsi: 0m)));

        Assert.DoesNotContain("Employer PF", text);
        Assert.DoesNotContain("Employer ESI", text);
        Assert.DoesNotContain("Employer contributions", text);
    }

    [Fact]
    public async Task Finalize_freezes_company_address_and_codes()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var runId = await FinalizedRunAsync(fixture);

        var run = await db.PayrollRuns.FindAsync(runId);
        Assert.Equal("1 Main Street, New Delhi, Delhi, 110001", run!.CompanyAddress);
        Assert.Equal("MH/123", run.PfEstablishmentCode);
        Assert.Equal("ESI-1", run.EsiCode);

        var company = await db.Companies.SingleAsync();
        company.AddressLine1 = "Changed Road";
        company.PfEstablishmentCode = "XX/999";
        await db.SaveChangesAsync();

        var row = db.PayrollEmployees
            .Include(item => item.Earnings)
            .Include(item => item.Deductions)
            .Single(item => item.PayrollRunId == runId);
        var snapshot = PayslipPdfService.FromRun((await db.PayrollRuns.FindAsync(runId))!, row, null);
        var text = PdfText(new PayslipPdfService().Render(snapshot));

        Assert.Equal("1 Main Street, New Delhi, Delhi, 110001", snapshot.CompanyAddress);
        Assert.Equal("MH/123", snapshot.PfEstablishmentCode);
        Assert.Contains("1 Main Street", text);
        Assert.DoesNotContain("Changed Road", text);
        Assert.DoesNotContain("XX/999", text);
    }

    [Fact]
    public async Task Reversed_run_payslip_keeps_both_watermarks()
    {
        var fixture = await FixtureAsync();
        await using var db = fixture.Db;
        var runId = await FinalizedRunAsync(fixture);
        var run = await db.PayrollRuns.FindAsync(runId);
        run!.Status = PayrollRunStatus.Reversed;
        await db.SaveChangesAsync();

        var file = (await new PayrollPayslipService(db, fixture.Tenant, new PayslipPdfService())
            .GetEmployeeAsync(runId, fixture.Employee.Id, null)).File!;
        var text = PdfText(file.Content);

        Assert.Contains(PayslipPdfService.BrandWatermark, text);
        Assert.Contains(PayslipPdfService.ReversedWatermark, text);
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
            AddressLine1 = "1 Main Street",
            City = "New Delhi",
            State = "Delhi",
            PostalCode = "110001",
            PfEstablishmentCode = "MH/123",
            EsiCode = "ESI-1",
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
        return new Fixture(db, new PayrollCalculationService(db, tenant), company, employee, tenant);
    }

    private static PayslipSnapshot SampleSlip(
        bool reversed = false,
        decimal employerPf = 1800m,
        decimal employerEsi = 585m) => new(
        "Acme",
        null,
        "1 Main Street, Pune, Maharashtra, 411001",
        "MH/123",
        "ESI-1",
        "Ada Lovelace",
        "EMP-01",
        "Engineer",
        31,
        Year,
        Month,
        [new PayslipLineSnapshot("Basic Salary", 20000m)],
        20000m,
        [new PayslipLineSnapshot("Provident Fund (PF)", 1800m)],
        1800m,
        18200m,
        employerPf,
        employerEsi,
        reversed,
        "Payment status: Unpaid");

    private static string PdfText(byte[] pdf)
    {
        using var document = PdfDocument.Open(pdf);
        return string.Join('\n', document.GetPages().Select(PageText));
    }

    private static string PageText(UglyToad.PdfPig.Content.Page page) =>
        string.Concat(page.Letters.Select(letter => letter.Value));
}
