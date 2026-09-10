using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Auth;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Payroll;
using MiniPayroll.Domain.Tenancy;

namespace MiniPayroll.Infrastructure.Persistence;

public sealed record PayrollLineDetail(string Name, PayrollLineKind Kind, decimal Amount, int SortOrder);

public sealed record PayrollEmployeeDetail(
    Guid EmployeeId,
    string EmployeeCode,
    string FullName,
    string Designation,
    int DaysEmployed,
    decimal DailyRate,
    decimal GrossEarnings,
    decimal TotalDeductions,
    decimal NetSalary,
    IReadOnlyList<PayrollLineDetail> Earnings,
    IReadOnlyList<PayrollLineDetail> Deductions,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors,
    SalaryPaymentStatus PaymentStatus,
    SalaryPaymentMode? PaymentMode,
    DateOnly? PaidOn,
    string? PaymentReference);

public sealed record PayrollRunDetail(
    Guid Id,
    int Year,
    int Month,
    PayrollRunStatus RunStatus,
    DailyRateMethod DailyRateMethod,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CalculatedAt,
    DateTimeOffset? FinalizedAt,
    IReadOnlyList<PayrollEmployeeDetail> Employees);

public enum PayrollRunStatusCode
{
    Success,
    CompanyNotFound,
    SetupIncomplete,
    SubscriptionReadOnly,
    InvalidPeriod,
    DuplicateRun,
    NotFound,
    RunLocked,
    InvalidInput,
    ConcurrencyConflict,
    NotCalculated,
    Forbidden
}

public sealed record PayrollRunResult(PayrollRunStatusCode Status, PayrollRunDetail? Run = null);

public sealed class PayrollCalculationService(
    MiniPayrollDbContext db,
    ITenantContext tenant)
{
    public const string MissingAttendanceError = "Attendance not entered for this employee.";

    public async Task<PayrollRunResult> CreateRunAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        var context = await GetContextAsync(true, cancellationToken);
        if (context.Status != PayrollRunStatusCode.Success || context.Company is null)
        {
            return new PayrollRunResult(context.Status);
        }

        var period = new PayrollPeriod(year, month);
        if (!period.IsValid)
        {
            return new PayrollRunResult(PayrollRunStatusCode.InvalidPeriod);
        }

        if (await db.PayrollRuns.AnyAsync(
            run => run.CompanyId == context.Company.Id
                && run.Year == year && run.Month == month
                && run.Status != PayrollRunStatus.Reversed,
            cancellationToken))
        {
            return new PayrollRunResult(PayrollRunStatusCode.DuplicateRun);
        }

        var newRun = new PayrollRun
        {
            Id = Guid.NewGuid(),
            CompanyId = context.Company.Id,
            Year = year,
            Month = month,
            Status = PayrollRunStatus.Draft,
            DailyRateMethod = context.Company.DailyRateMethod,
            CompanyName = context.Company.Name,
            CompanyLogoPath = context.Company.LogoPath,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.PayrollRuns.Add(newRun);
        AddAudit(AuditActions.PayrollRunCreate, $"{year}-{month:D2}");
        await db.SaveChangesAsync(cancellationToken);
        return new PayrollRunResult(PayrollRunStatusCode.Success, ToDetail(newRun, []));
    }

    /// <summary>Creates the draft run for the period when missing, then calculates it.</summary>
    public async Task<PayrollRunResult> CalculatePeriodAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        var context = await GetContextAsync(true, cancellationToken);
        if (context.Status != PayrollRunStatusCode.Success || context.Company is null)
        {
            return new PayrollRunResult(context.Status);
        }

        if (!new PayrollPeriod(year, month).IsValid)
        {
            return new PayrollRunResult(PayrollRunStatusCode.InvalidPeriod);
        }

        var run = await db.PayrollRuns.SingleOrDefaultAsync(
            item => item.CompanyId == context.Company.Id
                && item.Year == year && item.Month == month
                && item.Status != PayrollRunStatus.Reversed,
            cancellationToken);
        if (run is null)
        {
            var created = await CreateRunAsync(year, month, cancellationToken);
            if (created.Status != PayrollRunStatusCode.Success)
            {
                return created;
            }
            return await CalculateAsync(created.Run!.Id, cancellationToken);
        }

        return await CalculateAsync(run.Id, cancellationToken);
    }

    public async Task<PayrollRunResult> CalculateAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        var context = await GetContextAsync(true, cancellationToken);
        if (context.Status != PayrollRunStatusCode.Success || context.Company is null)
        {
            return new PayrollRunResult(context.Status);
        }
        var company = context.Company;

        var run = await db.PayrollRuns.SingleOrDefaultAsync(
            item => item.Id == runId && item.CompanyId == company.Id,
            cancellationToken);
        if (run is null)
        {
            return new PayrollRunResult(PayrollRunStatusCode.NotFound);
        }
        if (run.Status is PayrollRunStatus.Finalized or PayrollRunStatus.Reversed)
        {
            return new PayrollRunResult(PayrollRunStatusCode.RunLocked);
        }

        var period = new PayrollPeriod(run.Year, run.Month);

        var attendanceByEmployee = await db.MonthlyAttendance
            .Where(item => item.PayrollRunId == run.Id)
            .ToDictionaryAsync(item => item.EmployeeId, cancellationToken);
        var overtime = await db.Overtime
            .Where(item => item.PayrollRunId == run.Id)
            .ToListAsync(cancellationToken);
        var bonuses = await db.Bonuses
            .Where(item => item.PayrollRunId == run.Id)
            .ToListAsync(cancellationToken);
        var deductions = await db.Deductions
            .Where(item => item.PayrollRunId == run.Id)
            .ToListAsync(cancellationToken);

        var employees = (await db.Employees
                .Where(employee => employee.CompanyId == company.Id
                    && employee.Status != EmployeeStatus.Draft)
                .OrderBy(employee => employee.EmployeeCode)
                .ToListAsync(cancellationToken))
            .Where(employee => PayrollEligibility.IsEligible(
                employee.Status, employee.JoiningDate, employee.ExitDate, period))
            .ToList();

        var employeeIds = employees.Select(employee => employee.Id).ToList();
        var structureByEmployee = (await db.SalaryStructures
                .AsNoTracking()
                .Include(structure => structure.Components)
                .Where(structure => employeeIds.Contains(structure.EmployeeId)
                    && structure.EffectiveFrom <= period.LastDay)
                .ToListAsync(cancellationToken))
            .GroupBy(structure => structure.EmployeeId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(structure => structure.EffectiveFrom).First());

        var (previousYear, previousMonth) = run.Month == 1
            ? (run.Year - 1, 12)
            : (run.Year, run.Month - 1);
        var previousNets = await db.PayrollEmployees
            .Where(result => result.CompanyId == company.Id
                && result.PayrollRun.Year == previousYear
                && result.PayrollRun.Month == previousMonth
                && result.PayrollRun.Status != PayrollRunStatus.Reversed
                && result.Errors == null)
            .ToDictionaryAsync(result => result.EmployeeId, result => result.NetSalary, cancellationToken);

        // Idempotent recalculation: throw away previous results and rewrite.
        var existing = await db.PayrollEmployees
            .Include(result => result.Earnings)
            .Include(result => result.Deductions)
            .Where(result => result.PayrollRunId == run.Id)
            .ToListAsync(cancellationToken);
        db.PayrollEarnings.RemoveRange(existing.SelectMany(result => result.Earnings));
        db.PayrollDeductions.RemoveRange(existing.SelectMany(result => result.Deductions));
        db.PayrollEmployees.RemoveRange(existing);

        var rows = new List<PayrollEmployee>();
        foreach (var employee in employees)
        {
            PayrollEmployeeResult result;
            if (!attendanceByEmployee.TryGetValue(employee.Id, out var attendance))
            {
                result = new PayrollEmployeeResult([], 0m, 0m, 0m, 0, 0m, [MissingAttendanceError], []);
            }
            else
            {
                structureByEmployee.TryGetValue(employee.Id, out var structure);
                result = PayrollCalculator.Calculate(new PayrollEmployeeInput(
                    period,
                    run.DailyRateMethod,
                    employee.JoiningDate,
                    employee.ExitDate,
                    new PayrollAttendance(
                        attendance.WorkingDays,
                        attendance.Present,
                        attendance.PaidLeave,
                        attendance.UnpaidLeave),
                    structure?.EffectiveFrom,
                    structure?.Components
                        .OrderBy(component => component.SortOrder)
                        .Select(component => new PayrollStructureLine(
                            component.Name, component.Type, component.ValueType,
                            component.Value, component.SortOrder))
                        .ToList(),
                    overtime.Where(entry => entry.EmployeeId == employee.Id)
                        .Select(entry => new PayrollOvertimeEntry(
                            entry.Hours, entry.Rate ?? employee.OvertimeRate))
                        .ToList(),
                    bonuses.Where(entry => entry.EmployeeId == employee.Id)
                        .Select(entry => new PayrollAmountEntry(BonusName(entry.Type), entry.Amount))
                        .ToList(),
                    deductions.Where(entry => entry.EmployeeId == employee.Id)
                        .Select(entry => new PayrollAmountEntry(DeductionName(entry.Type), entry.Amount))
                        .ToList(),
                    previousNets.TryGetValue(employee.Id, out var previousNet) ? previousNet : null));
            }

            var row = ToRow(run, employee, result);
            db.PayrollEmployees.Add(row);
            rows.Add(row);
        }

        var hasBlockingErrors = rows.Count == 0 || rows.Any(row => row.Errors is not null);
        run.Status = hasBlockingErrors ? PayrollRunStatus.Draft : PayrollRunStatus.Calculated;
        run.CalculatedAt = hasBlockingErrors ? null : DateTimeOffset.UtcNow;

        AddAudit(
            AuditActions.PayrollRunCalculate,
            $"{run.Year}-{run.Month:D2} employees {rows.Count} status {run.Status}");
        await db.SaveChangesAsync(cancellationToken);

        return new PayrollRunResult(PayrollRunStatusCode.Success, ToDetail(run, rows));
    }

    public async Task<PayrollRunResult> FinalizeAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        var context = await GetContextAsync(true, cancellationToken);
        if (context.Status != PayrollRunStatusCode.Success || context.Company is null)
        {
            return new PayrollRunResult(context.Status);
        }

        var run = await db.PayrollRuns.SingleOrDefaultAsync(
            item => item.Id == runId && item.CompanyId == context.Company.Id,
            cancellationToken);
        if (run is null)
        {
            return new PayrollRunResult(PayrollRunStatusCode.NotFound);
        }
        if (run.Status is PayrollRunStatus.Finalized or PayrollRunStatus.Reversed)
        {
            return new PayrollRunResult(PayrollRunStatusCode.RunLocked);
        }
        if (run.Status != PayrollRunStatus.Calculated)
        {
            return new PayrollRunResult(PayrollRunStatusCode.NotCalculated);
        }

        var rows = await db.PayrollEmployees
            .Include(result => result.Earnings)
            .Include(result => result.Deductions)
            .Where(result => result.PayrollRunId == run.Id)
            .ToListAsync(cancellationToken);
        if (rows.Count == 0 || rows.Any(row => row.Errors is not null))
        {
            return new PayrollRunResult(PayrollRunStatusCode.NotCalculated);
        }

        var employees = await db.Employees
            .Where(employee => employee.CompanyId == context.Company.Id)
            .ToDictionaryAsync(employee => employee.Id, cancellationToken);

        run.Status = PayrollRunStatus.Finalized;
        run.FinalizedAt = DateTimeOffset.UtcNow;
        run.FinalizedByUserId = tenant.UserId;
        run.CompanyName = context.Company.Name;
        run.CompanyLogoPath = context.Company.LogoPath;
        foreach (var row in rows)
        {
            if (employees.TryGetValue(row.EmployeeId, out var employee))
            {
                row.Designation = employee.Designation;
            }
        }

        AddAudit(AuditActions.PayrollRunFinalize, $"{run.Year}-{run.Month:D2}");
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new PayrollRunResult(PayrollRunStatusCode.ConcurrencyConflict);
        }

        return new PayrollRunResult(PayrollRunStatusCode.Success, ToDetail(run, rows));
    }

    public async Task<PayrollRunResult> ReverseAsync(
        Guid companyId,
        Guid runId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        if (!tenant.IsSuperadmin)
        {
            return new PayrollRunResult(PayrollRunStatusCode.Forbidden);
        }

        var trimmed = reason?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return new PayrollRunResult(PayrollRunStatusCode.InvalidInput);
        }

        var run = await db.PayrollRuns.SingleOrDefaultAsync(
            item => item.Id == runId && item.CompanyId == companyId,
            cancellationToken);
        if (run is null)
        {
            return new PayrollRunResult(PayrollRunStatusCode.NotFound);
        }
        if (run.Status != PayrollRunStatus.Finalized)
        {
            return new PayrollRunResult(PayrollRunStatusCode.RunLocked);
        }

        var rows = await db.PayrollEmployees
            .Include(result => result.Earnings)
            .Include(result => result.Deductions)
            .Where(result => result.PayrollRunId == run.Id)
            .ToListAsync(cancellationToken);

        run.Status = PayrollRunStatus.Reversed;
        run.ReversedAt = DateTimeOffset.UtcNow;
        run.ReversedByUserId = tenant.UserId;
        run.ReversalReason = trimmed.Length > 500 ? trimmed[..500] : trimmed;

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            CompanyId = run.CompanyId,
            ActorUserId = tenant.UserId,
            Action = AuditActions.PayrollRunReverse,
            Details = $"{run.Year}-{run.Month:D2} {run.ReversalReason}",
            OccurredAt = DateTimeOffset.UtcNow
        });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new PayrollRunResult(PayrollRunStatusCode.ConcurrencyConflict);
        }

        return new PayrollRunResult(PayrollRunStatusCode.Success, ToDetail(run, rows));
    }

    public async Task<PayrollHistoryResult> ListCompanyRunsAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        if (!tenant.IsSuperadmin)
        {
            return new PayrollHistoryResult(PayrollRunStatusCode.Forbidden);
        }

        var exists = await db.Companies.AnyAsync(item => item.Id == companyId, cancellationToken);
        if (!exists)
        {
            return new PayrollHistoryResult(PayrollRunStatusCode.CompanyNotFound);
        }

        var runs = await db.PayrollRuns
            .AsNoTracking()
            .Where(run => run.CompanyId == companyId)
            .OrderByDescending(run => run.Year)
            .ThenByDescending(run => run.Month)
            .ThenByDescending(run => run.CreatedAt)
            .Select(run => new PayrollHistoryItem(
                run.Id,
                run.Year,
                run.Month,
                run.Status,
                run.Results.Count,
                run.Results.Sum(result => result.GrossEarnings),
                run.Results.Sum(result => result.TotalDeductions),
                run.Results.Sum(result => result.NetSalary),
                run.CreatedAt,
                run.CalculatedAt,
                run.FinalizedAt))
            .ToListAsync(cancellationToken);

        return new PayrollHistoryResult(PayrollRunStatusCode.Success, runs);
    }

    private static PayrollEmployee ToRow(PayrollRun run, Employee employee, PayrollEmployeeResult result)
    {
        var row = new PayrollEmployee
        {
            Id = Guid.NewGuid(),
            CompanyId = run.CompanyId,
            PayrollRunId = run.Id,
            EmployeeId = employee.Id,
            EmployeeCode = employee.EmployeeCode,
            FullName = employee.FullName,
            Designation = employee.Designation,
            DaysEmployed = result.DaysEmployed,
            DailyRate = decimal.Round(result.DailyRate, 6, MidpointRounding.AwayFromZero),
            GrossEarnings = result.GrossEarnings,
            TotalDeductions = result.TotalDeductions,
            NetSalary = result.NetSalary,
            Warnings = result.Warnings.Count > 0 ? string.Join("\n", result.Warnings) : null,
            Errors = result.Errors.Count > 0 ? string.Join("\n", result.Errors) : null
        };

        var earningOrder = 0;
        var deductionOrder = 0;
        foreach (var line in result.Lines)
        {
            if (line.Type == SalaryComponentType.Earning)
            {
                row.Earnings.Add(new PayrollEarning
                {
                    Id = Guid.NewGuid(),
                    CompanyId = run.CompanyId,
                    PayrollEmployeeId = row.Id,
                    Name = line.Name,
                    Kind = line.Kind,
                    Amount = line.Amount,
                    SortOrder = earningOrder++
                });
            }
            else
            {
                row.Deductions.Add(new PayrollDeduction
                {
                    Id = Guid.NewGuid(),
                    CompanyId = run.CompanyId,
                    PayrollEmployeeId = row.Id,
                    Name = line.Name,
                    Kind = line.Kind,
                    Amount = line.Amount,
                    SortOrder = deductionOrder++
                });
            }
        }
        return row;
    }

    private static PayrollRunDetail ToDetail(PayrollRun run, IReadOnlyList<PayrollEmployee> rows) => new(
        run.Id,
        run.Year,
        run.Month,
        run.Status,
        run.DailyRateMethod,
        run.CreatedAt,
        run.CalculatedAt,
        run.FinalizedAt,
        rows.OrderBy(row => row.EmployeeCode)
            .Select(row => new PayrollEmployeeDetail(
                row.EmployeeId,
                row.EmployeeCode,
                row.FullName,
                row.Designation,
                row.DaysEmployed,
                row.DailyRate,
                row.GrossEarnings,
                row.TotalDeductions,
                row.NetSalary,
                row.Earnings.OrderBy(line => line.SortOrder)
                    .Select(line => new PayrollLineDetail(line.Name, line.Kind, line.Amount, line.SortOrder))
                    .ToList(),
                row.Deductions.OrderBy(line => line.SortOrder)
                    .Select(line => new PayrollLineDetail(line.Name, line.Kind, line.Amount, line.SortOrder))
                    .ToList(),
                Split(row.Warnings),
                Split(row.Errors),
                row.PaymentStatus,
                row.PaymentMode,
                row.PaidOn,
                row.PaymentReference))
            .ToList());

    private static IReadOnlyList<string> Split(string? joined) =>
        string.IsNullOrEmpty(joined) ? [] : joined.Split('\n');

    private static string BonusName(BonusType type) => type switch
    {
        BonusType.Festival => "Festival Bonus",
        BonusType.Performance => "Performance Bonus",
        BonusType.Attendance => "Attendance Bonus",
        BonusType.Incentive => "Incentive",
        _ => "Bonus"
    };

    private static string DeductionName(OneTimeDeductionType type) => type switch
    {
        OneTimeDeductionType.AdvanceRecovery => "Advance Recovery",
        OneTimeDeductionType.LoanInstallment => "Loan Installment",
        OneTimeDeductionType.Tds => "TDS",
        _ => "Other Deduction"
    };

    private async Task<(PayrollRunStatusCode Status, Company? Company)> GetContextAsync(
        bool requireMutation,
        CancellationToken cancellationToken)
    {
        if (tenant.IsSuperadmin || tenant.CompanyId is not { } companyId)
        {
            return (PayrollRunStatusCode.CompanyNotFound, null);
        }

        var company = await db.Companies.Include(item => item.Subscription)
            .SingleOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        if (company is null)
        {
            return (PayrollRunStatusCode.CompanyNotFound, null);
        }
        if (!company.IsSetupComplete)
        {
            return (PayrollRunStatusCode.SetupIncomplete, company);
        }
        if (requireMutation && !SubscriptionMutationRules.CanMutate(company.Subscription?.Status))
        {
            return (PayrollRunStatusCode.SubscriptionReadOnly, company);
        }
        return (PayrollRunStatusCode.Success, company);
    }

    private void AddAudit(string action, string details)
    {
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            CompanyId = tenant.CompanyId,
            ActorUserId = tenant.UserId,
            Action = action,
            Details = details,
            OccurredAt = DateTimeOffset.UtcNow
        });
    }
}
