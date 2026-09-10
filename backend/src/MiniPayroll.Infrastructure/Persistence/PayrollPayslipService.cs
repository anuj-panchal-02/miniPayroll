using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Tenancy;

namespace MiniPayroll.Infrastructure.Persistence;

public sealed record PayslipFile(byte[] Content, string FileName);

public sealed record PayslipFileResult(PayrollRunStatusCode Status, PayslipFile? File = null);

public sealed class PayrollPayslipService(
    MiniPayrollDbContext db,
    ITenantContext tenant,
    PayslipPdfService pdf)
{
    public async Task<PayslipFileResult> GetEmployeeAsync(
        Guid runId,
        Guid employeeId,
        Func<string?, byte[]?>? logoReader,
        CancellationToken cancellationToken = default)
    {
        var loaded = await LoadAsync(runId, cancellationToken);
        if (loaded.Status != PayrollRunStatusCode.Success || loaded.Run is null)
        {
            return new PayslipFileResult(loaded.Status);
        }

        var row = loaded.Rows.SingleOrDefault(item => item.EmployeeId == employeeId);
        if (row is null)
        {
            return new PayslipFileResult(PayrollRunStatusCode.NotFound);
        }

        var slip = PayslipPdfService.FromRun(loaded.Run, row, logoReader?.Invoke(loaded.Run.CompanyLogoPath));
        var bytes = pdf.Render(slip);
        return new PayslipFileResult(
            PayrollRunStatusCode.Success,
            new PayslipFile(bytes, $"payslip-{row.EmployeeCode}-{loaded.Run.Year}-{loaded.Run.Month:D2}.pdf"));
    }

    public async Task<PayslipFileResult> GetCombinedAsync(
        Guid runId,
        Func<string?, byte[]?>? logoReader,
        CancellationToken cancellationToken = default)
    {
        var loaded = await LoadAsync(runId, cancellationToken);
        if (loaded.Status != PayrollRunStatusCode.Success || loaded.Run is null)
        {
            return new PayslipFileResult(loaded.Status);
        }

        var logo = logoReader?.Invoke(loaded.Run.CompanyLogoPath);
        var slips = loaded.Rows
            .OrderBy(row => row.EmployeeCode)
            .Select(row => PayslipPdfService.FromRun(loaded.Run, row, logo))
            .ToList();
        var bytes = pdf.RenderCombined(slips);
        return new PayslipFileResult(
            PayrollRunStatusCode.Success,
            new PayslipFile(bytes, $"payslips-{loaded.Run.Year}-{loaded.Run.Month:D2}.pdf"));
    }

    private async Task<(PayrollRunStatusCode Status, PayrollRun? Run, List<PayrollEmployee> Rows)> LoadAsync(
        Guid runId,
        CancellationToken cancellationToken)
    {
        if (tenant.IsSuperadmin || tenant.CompanyId is not { } companyId)
        {
            return (PayrollRunStatusCode.CompanyNotFound, null, []);
        }

        var company = await db.Companies.SingleOrDefaultAsync(
            item => item.Id == companyId, cancellationToken);
        if (company is null)
        {
            return (PayrollRunStatusCode.CompanyNotFound, null, []);
        }
        if (!company.IsSetupComplete)
        {
            return (PayrollRunStatusCode.SetupIncomplete, null, []);
        }

        var run = await db.PayrollRuns.SingleOrDefaultAsync(
            item => item.Id == runId && item.CompanyId == company.Id,
            cancellationToken);
        if (run is null)
        {
            return (PayrollRunStatusCode.NotFound, null, []);
        }
        if (run.Status is not (PayrollRunStatus.Finalized or PayrollRunStatus.Reversed))
        {
            return (PayrollRunStatusCode.NotCalculated, null, []);
        }

        var rows = await db.PayrollEmployees
            .Include(item => item.Earnings)
            .Include(item => item.Deductions)
            .Where(item => item.PayrollRunId == run.Id)
            .ToListAsync(cancellationToken);
        return (PayrollRunStatusCode.Success, run, rows);
    }
}
