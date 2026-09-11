using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Auth;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Payroll;
using MiniPayroll.Domain.Tenancy;

namespace MiniPayroll.Infrastructure.Persistence;

public sealed record PayrollAttendanceInput(
    Guid EmployeeId,
    decimal WorkingDays,
    decimal Present,
    decimal PaidLeave,
    decimal UnpaidLeave);

public sealed record PayrollOvertimeInput(
    Guid EmployeeId,
    decimal Hours,
    decimal? Rate,
    string? Notes);

public sealed record PayrollBonusInput(
    Guid EmployeeId,
    BonusType Type,
    decimal Amount,
    string? Notes);

public sealed record PayrollDeductionInput(
    Guid EmployeeId,
    OneTimeDeductionType Type,
    decimal Amount,
    string? Notes);

public sealed record PayrollInputsPayload(
    IReadOnlyList<PayrollAttendanceInput>? Attendance,
    IReadOnlyList<PayrollOvertimeInput>? Overtime,
    IReadOnlyList<PayrollBonusInput>? Bonuses,
    IReadOnlyList<PayrollDeductionInput>? Deductions);

public sealed record PayrollAttendanceDetail(
    Guid EmployeeId,
    decimal WorkingDays,
    decimal Present,
    decimal PaidLeave,
    decimal UnpaidLeave);

public sealed record PayrollOvertimeDetail(
    Guid Id,
    Guid EmployeeId,
    decimal Hours,
    decimal? Rate,
    string? Notes);

public sealed record PayrollBonusDetail(
    Guid Id,
    Guid EmployeeId,
    BonusType Type,
    decimal Amount,
    string? Notes);

public sealed record PayrollDeductionDetail(
    Guid Id,
    Guid EmployeeId,
    OneTimeDeductionType Type,
    decimal Amount,
    string? Notes);

public sealed record PayrollRosterEmployee(
    Guid EmployeeId,
    string EmployeeCode,
    string FullName,
    EmployeeStatus Status,
    DateOnly? JoiningDate,
    DateOnly? ExitDate,
    bool HasStructure,
    decimal? OvertimeRate,
    PayrollAttendanceDetail? Attendance,
    IReadOnlyList<PayrollOvertimeDetail> Overtime,
    IReadOnlyList<PayrollBonusDetail> Bonuses,
    IReadOnlyList<PayrollDeductionDetail> Deductions);

public sealed record PayrollPeriodRunSummary(
    Guid Id,
    PayrollRunStatus Status,
    DailyRateMethod DailyRateMethod,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CalculatedAt,
    DateTimeOffset? FinalizedAt);

public sealed record PayrollTotals(
    decimal GrossEarnings,
    decimal TotalDeductions,
    decimal NetSalary,
    int EmployeeCount,
    int WarningCount,
    int ErrorCount);

public sealed record PayrollPeriodDetail(
    int Year,
    int Month,
    int WorkingDaysPerMonth,
    PayrollPeriodRunSummary? Run,
    IReadOnlyList<PayrollRosterEmployee> Employees,
    IReadOnlyList<PayrollEmployeeDetail> Results,
    PayrollTotals? Totals);

public sealed record PayrollPeriodResult(
    PayrollRunStatusCode Status,
    PayrollPeriodDetail? Period = null);

public sealed record PayrollHistoryItem(
    Guid Id,
    int Year,
    int Month,
    PayrollRunStatus Status,
    int EmployeeCount,
    decimal GrossEarnings,
    decimal TotalDeductions,
    decimal NetSalary,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CalculatedAt,
    DateTimeOffset? FinalizedAt);

public sealed record PayrollHistoryResult(
    PayrollRunStatusCode Status,
    IReadOnlyList<PayrollHistoryItem>? Runs = null);

public sealed record PayrollPaymentPayload(
    SalaryPaymentStatus PaymentStatus,
    SalaryPaymentMode? PaymentMode,
    DateOnly? PaidOn,
    string? PaymentReference);

public sealed class PayrollInputService(
    MiniPayrollDbContext db,
    ITenantContext tenant)
{
    public async Task<PayrollPeriodResult> GetPeriodAsync(
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        var context = await GetContextAsync(false, cancellationToken);
        if (context.Status != PayrollRunStatusCode.Success || context.Company is null)
        {
            return new PayrollPeriodResult(context.Status);
        }

        var period = new PayrollPeriod(year, month);
        if (!period.IsValid)
        {
            return new PayrollPeriodResult(PayrollRunStatusCode.InvalidPeriod);
        }

        var run = await db.PayrollRuns.SingleOrDefaultAsync(
            item => item.CompanyId == context.Company.Id
                && item.Year == year && item.Month == month
                && item.Status != PayrollRunStatus.Reversed,
            cancellationToken);

        return new PayrollPeriodResult(
            PayrollRunStatusCode.Success,
            await BuildPeriodAsync(context.Company, period, run, cancellationToken));
    }

    public async Task<PayrollHistoryResult> ListRunsAsync(CancellationToken cancellationToken = default)
    {
        var context = await GetContextAsync(false, cancellationToken);
        if (context.Status != PayrollRunStatusCode.Success || context.Company is null)
        {
            return new PayrollHistoryResult(context.Status);
        }

        var runs = await db.PayrollRuns
            .AsNoTracking()
            .Where(run => run.CompanyId == context.Company.Id)
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

    public async Task<PayrollPeriodResult> SetPaymentAsync(
        Guid runId,
        Guid employeeId,
        PayrollPaymentPayload? input,
        CancellationToken cancellationToken = default)
    {
        var context = await GetContextAsync(true, cancellationToken);
        if (context.Status != PayrollRunStatusCode.Success || context.Company is null)
        {
            return new PayrollPeriodResult(context.Status);
        }

        var run = await db.PayrollRuns.SingleOrDefaultAsync(
            item => item.Id == runId && item.CompanyId == context.Company.Id,
            cancellationToken);
        if (run is null)
        {
            return new PayrollPeriodResult(PayrollRunStatusCode.NotFound);
        }
        if (run.Status != PayrollRunStatus.Finalized)
        {
            return new PayrollPeriodResult(PayrollRunStatusCode.RunLocked);
        }

        var payload = input ?? new PayrollPaymentPayload(
            SalaryPaymentStatus.Unpaid, null, null, null);
        if (payload.PaymentStatus == SalaryPaymentStatus.Paid
            && (payload.PaymentMode is null || payload.PaidOn is null))
        {
            return new PayrollPeriodResult(PayrollRunStatusCode.InvalidInput);
        }

        var row = await db.PayrollEmployees.SingleOrDefaultAsync(
            item => item.PayrollRunId == run.Id && item.EmployeeId == employeeId,
            cancellationToken);
        if (row is null)
        {
            return new PayrollPeriodResult(PayrollRunStatusCode.NotFound);
        }

        if (payload.PaymentStatus == SalaryPaymentStatus.Paid)
        {
            row.PaymentStatus = SalaryPaymentStatus.Paid;
            row.PaymentMode = payload.PaymentMode;
            row.PaidOn = payload.PaidOn;
            row.PaymentReference = Trim(payload.PaymentReference);
        }
        else
        {
            row.PaymentStatus = SalaryPaymentStatus.Unpaid;
            row.PaymentMode = null;
            row.PaidOn = null;
            row.PaymentReference = null;
        }

        AddAudit(
            AuditActions.PayrollPaymentUpdate,
            $"{run.Year}-{run.Month:D2} {row.EmployeeCode} {row.PaymentStatus}");
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new PayrollPeriodResult(PayrollRunStatusCode.ConcurrencyConflict);
        }

        var period = new PayrollPeriod(run.Year, run.Month);
        return new PayrollPeriodResult(
            PayrollRunStatusCode.Success,
            await BuildPeriodAsync(context.Company, period, run, cancellationToken));
    }

    public async Task<PayrollPeriodResult> SaveInputsAsync(
        Guid runId,
        PayrollInputsPayload? input,
        CancellationToken cancellationToken = default)
    {
        var context = await GetContextAsync(true, cancellationToken);
        if (context.Status != PayrollRunStatusCode.Success || context.Company is null)
        {
            return new PayrollPeriodResult(context.Status);
        }
        var company = context.Company;

        var run = await db.PayrollRuns.SingleOrDefaultAsync(
            item => item.Id == runId && item.CompanyId == company.Id,
            cancellationToken);
        if (run is null)
        {
            return new PayrollPeriodResult(PayrollRunStatusCode.NotFound);
        }
        if (run.Status is PayrollRunStatus.Finalized or PayrollRunStatus.Reversed)
        {
            return new PayrollPeriodResult(PayrollRunStatusCode.RunLocked);
        }

        var period = new PayrollPeriod(run.Year, run.Month);
        var eligible = (await LoadEligibleAsync(company.Id, period, cancellationToken))
            .ToDictionary(employee => employee.Id);
        var payload = input ?? new PayrollInputsPayload(null, null, null, null);

        if (!IsValid(payload, eligible.Keys))
        {
            return new PayrollPeriodResult(PayrollRunStatusCode.InvalidInput);
        }

        var existingAttendance = await db.MonthlyAttendance
            .Where(item => item.PayrollRunId == run.Id)
            .ToListAsync(cancellationToken);
        var existingOvertime = await db.Overtime
            .Where(item => item.PayrollRunId == run.Id)
            .ToListAsync(cancellationToken);
        var existingBonuses = await db.Bonuses
            .Where(item => item.PayrollRunId == run.Id)
            .ToListAsync(cancellationToken);
        var existingDeductions = await db.Deductions
            .Where(item => item.PayrollRunId == run.Id)
            .ToListAsync(cancellationToken);

        db.MonthlyAttendance.RemoveRange(existingAttendance);
        db.Overtime.RemoveRange(existingOvertime);
        db.Bonuses.RemoveRange(existingBonuses);
        db.Deductions.RemoveRange(existingDeductions);

        foreach (var row in payload.Attendance ?? [])
        {
            db.MonthlyAttendance.Add(new MonthlyAttendance
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                PayrollRunId = run.Id,
                EmployeeId = row.EmployeeId,
                WorkingDays = row.WorkingDays,
                Present = row.Present,
                PaidLeave = row.PaidLeave,
                UnpaidLeave = row.UnpaidLeave
            });
        }

        foreach (var row in payload.Overtime ?? [])
        {
            db.Overtime.Add(new Overtime
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                PayrollRunId = run.Id,
                EmployeeId = row.EmployeeId,
                Hours = row.Hours,
                Rate = row.Rate,
                Notes = Trim(row.Notes)
            });
        }

        foreach (var row in payload.Bonuses ?? [])
        {
            db.Bonuses.Add(new Bonus
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                PayrollRunId = run.Id,
                EmployeeId = row.EmployeeId,
                Type = row.Type,
                Amount = decimal.Round(row.Amount, 2, MidpointRounding.AwayFromZero),
                Notes = Trim(row.Notes)
            });
        }

        foreach (var row in payload.Deductions ?? [])
        {
            db.Deductions.Add(new Deduction
            {
                Id = Guid.NewGuid(),
                CompanyId = company.Id,
                PayrollRunId = run.Id,
                EmployeeId = row.EmployeeId,
                Type = row.Type,
                Amount = decimal.Round(row.Amount, 2, MidpointRounding.AwayFromZero),
                Notes = Trim(row.Notes)
            });
        }

        if (run.Status == PayrollRunStatus.Calculated)
        {
            run.Status = PayrollRunStatus.Draft;
            run.CalculatedAt = null;
        }

        AddAudit(AuditActions.PayrollInputsSave, $"{run.Year}-{run.Month:D2}");

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new PayrollPeriodResult(PayrollRunStatusCode.ConcurrencyConflict);
        }

        return new PayrollPeriodResult(
            PayrollRunStatusCode.Success,
            await BuildPeriodAsync(company, period, run, cancellationToken));
    }

    internal static bool IsValid(PayrollInputsPayload payload, IReadOnlyCollection<Guid> eligibleIds)
    {
        var attendanceIds = new HashSet<Guid>();
        foreach (var row in payload.Attendance ?? [])
        {
            if (!eligibleIds.Contains(row.EmployeeId)
                || !attendanceIds.Add(row.EmployeeId)
                || !PayrollInputRules.IsHalfDayQuantity(row.WorkingDays)
                || !PayrollInputRules.IsHalfDayQuantity(row.Present)
                || !PayrollInputRules.IsHalfDayQuantity(row.PaidLeave)
                || !PayrollInputRules.IsHalfDayQuantity(row.UnpaidLeave))
            {
                return false;
            }
        }

        foreach (var row in payload.Overtime ?? [])
        {
            if (!eligibleIds.Contains(row.EmployeeId)
                || !PayrollInputRules.IsPositiveAmount(row.Hours)
                || (row.Rate is { } rate && !PayrollInputRules.IsPositiveAmount(rate))
                || !PayrollInputRules.IsValidNotes(row.Notes))
            {
                return false;
            }
        }

        foreach (var row in payload.Bonuses ?? [])
        {
            if (!eligibleIds.Contains(row.EmployeeId)
                || !Enum.IsDefined(row.Type)
                || !PayrollInputRules.IsPositiveAmount(row.Amount)
                || !PayrollInputRules.IsValidNotes(row.Notes))
            {
                return false;
            }
        }

        foreach (var row in payload.Deductions ?? [])
        {
            if (!eligibleIds.Contains(row.EmployeeId)
                || !Enum.IsDefined(row.Type)
                || !PayrollInputRules.IsPositiveAmount(row.Amount)
                || !PayrollInputRules.IsValidNotes(row.Notes))
            {
                return false;
            }
        }

        return true;
    }

    private async Task<PayrollPeriodDetail> BuildPeriodAsync(
        Company company,
        PayrollPeriod period,
        PayrollRun? run,
        CancellationToken cancellationToken)
    {
        var employees = await LoadEligibleAsync(company.Id, period, cancellationToken);
        var employeeIds = employees.Select(employee => employee.Id).ToList();
        var structuredIds = (await db.SalaryStructures
                .AsNoTracking()
                .Where(structure => employeeIds.Contains(structure.EmployeeId)
                    && structure.EffectiveFrom <= period.LastDay)
                .Select(structure => structure.EmployeeId)
                .Distinct()
                .ToListAsync(cancellationToken))
            .ToHashSet();

        Dictionary<Guid, MonthlyAttendance> attendanceByEmployee = [];
        List<Overtime> overtime = [];
        List<Bonus> bonuses = [];
        List<Deduction> deductions = [];
        List<PayrollEmployee> results = [];

        if (run is not null)
        {
            attendanceByEmployee = await db.MonthlyAttendance
                .Where(item => item.PayrollRunId == run.Id)
                .ToDictionaryAsync(item => item.EmployeeId, cancellationToken);
            overtime = await db.Overtime
                .Where(item => item.PayrollRunId == run.Id)
                .ToListAsync(cancellationToken);
            bonuses = await db.Bonuses
                .Where(item => item.PayrollRunId == run.Id)
                .ToListAsync(cancellationToken);
            deductions = await db.Deductions
                .Where(item => item.PayrollRunId == run.Id)
                .ToListAsync(cancellationToken);
            results = await db.PayrollEmployees
                .Include(item => item.Earnings)
                .Include(item => item.Deductions)
                .Where(item => item.PayrollRunId == run.Id)
                .ToListAsync(cancellationToken);
        }

        var roster = employees.Select(employee => new PayrollRosterEmployee(
            employee.Id,
            employee.EmployeeCode,
            employee.FullName,
            employee.Status,
            employee.JoiningDate,
            employee.ExitDate,
            structuredIds.Contains(employee.Id),
            employee.OvertimeRate,
            attendanceByEmployee.TryGetValue(employee.Id, out var attendance)
                ? new PayrollAttendanceDetail(
                    employee.Id,
                    attendance.WorkingDays,
                    attendance.Present,
                    attendance.PaidLeave,
                    attendance.UnpaidLeave)
                : null,
            overtime.Where(item => item.EmployeeId == employee.Id)
                .Select(item => new PayrollOvertimeDetail(
                    item.Id, item.EmployeeId, item.Hours, item.Rate, item.Notes))
                .ToList(),
            bonuses.Where(item => item.EmployeeId == employee.Id)
                .Select(item => new PayrollBonusDetail(
                    item.Id, item.EmployeeId, item.Type, item.Amount, item.Notes))
                .ToList(),
            deductions.Where(item => item.EmployeeId == employee.Id)
                .Select(item => new PayrollDeductionDetail(
                    item.Id, item.EmployeeId, item.Type, item.Amount, item.Notes))
                .ToList()))
            .ToList();

        var resultDetails = results
            .OrderBy(item => item.EmployeeCode)
            .Select(ToEmployeeDetail)
            .ToList();
        var totals = resultDetails.Count == 0
            ? null
            : new PayrollTotals(
                resultDetails.Sum(item => item.GrossEarnings),
                resultDetails.Sum(item => item.TotalDeductions),
                resultDetails.Sum(item => item.NetSalary),
                resultDetails.Count,
                resultDetails.Count(item => item.Warnings.Count > 0),
                resultDetails.Count(item => item.Errors.Count > 0));

        return new PayrollPeriodDetail(
            period.Year,
            period.Month,
            company.WorkingDaysPerMonth,
            run is null
                ? null
                : new PayrollPeriodRunSummary(
                    run.Id, run.Status, run.DailyRateMethod, run.CreatedAt, run.CalculatedAt,
                    run.FinalizedAt),
            roster,
            resultDetails,
            totals);
    }

    private async Task<List<Employee>> LoadEligibleAsync(
        Guid companyId,
        PayrollPeriod period,
        CancellationToken cancellationToken) =>
        (await db.Employees
            .Where(employee => employee.CompanyId == companyId
                && employee.Status != EmployeeStatus.Draft)
            .OrderBy(employee => employee.EmployeeCode)
            .ToListAsync(cancellationToken))
        .Where(employee => PayrollEligibility.IsEligible(
            employee.Status, employee.JoiningDate, employee.ExitDate, period))
        .ToList();

    private static PayrollEmployeeDetail ToEmployeeDetail(PayrollEmployee row) => new(
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
            .Select(line => new PayrollLineDetail(
                line.Name, line.Kind, line.Amount, line.SortOrder,
                line.ComputedAmount, line.StatutoryKind))
            .ToList(),
        Split(row.Warnings),
        Split(row.Errors),
        row.PaymentStatus,
        row.PaymentMode,
        row.PaidOn,
        row.PaymentReference,
        row.EmployerPf,
        row.EmployerEsi);

    private static IReadOnlyList<string> Split(string? joined) =>
        string.IsNullOrEmpty(joined) ? [] : joined.Split('\n');

    private static string? Trim(string? notes)
    {
        var value = notes?.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

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
