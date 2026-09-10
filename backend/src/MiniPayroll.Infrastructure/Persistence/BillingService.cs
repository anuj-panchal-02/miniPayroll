using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Billing;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Payroll;
using MiniPayroll.Domain.Tenancy;

namespace MiniPayroll.Infrastructure.Persistence;

public enum BillingStatusCode
{
    Success,
    Forbidden,
    CompanyNotFound,
    InvalidInput,
    InvalidPeriod,
    NotActivated
}

public sealed record BillingPaymentItem(
    Guid Id,
    decimal Amount,
    DateTimeOffset PaidOn,
    string PaymentMode,
    string? InvoiceGstReference,
    DateTimeOffset RecordedAt);

public sealed record BillingPeriodSummary(
    string BillingPeriod,
    int Year,
    int Month,
    int BillableEmployees,
    BillableSource BillableSource,
    decimal PricePerEmployee,
    decimal AmountDue,
    bool Prorated,
    bool IsEstimated,
    DateTimeOffset DueDate,
    bool IsOverdue,
    bool IsPastGrace,
    decimal PaidAmount,
    decimal Remaining,
    IReadOnlyList<BillingPaymentItem> Payments);

public sealed record CompanyBilling(
    string PlanName,
    decimal PricePerEmployee,
    int GracePeriodDays,
    IReadOnlyList<BillingPeriodSummary> Periods);

public sealed record BillingResult(BillingStatusCode Status, CompanyBilling? Billing = null);

public sealed record RecordPaymentRequest(
    string BillingPeriod,
    decimal Amount,
    DateTimeOffset PaidOn,
    string PaymentMode,
    string? InvoiceGstReference);

public sealed class BillingService
{
    private readonly MiniPayrollDbContext db;
    private readonly ITenantContext tenant;
    private readonly TimeProvider time;

    public BillingService(MiniPayrollDbContext db, ITenantContext tenant)
        : this(db, tenant, TimeProvider.System)
    {
    }

    public BillingService(MiniPayrollDbContext db, ITenantContext tenant, TimeProvider time)
    {
        this.db = db;
        this.tenant = tenant;
        this.time = time;
    }

    public async Task<BillingResult> GetAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        if (!tenant.IsSuperadmin)
        {
            return new BillingResult(BillingStatusCode.Forbidden);
        }

        return await BuildForCompanyAsync(companyId, cancellationToken);
    }

    public async Task<BillingResult> GetOwnAsync(CancellationToken cancellationToken = default)
    {
        if (tenant.IsSuperadmin || tenant.CompanyId is not { } companyId)
        {
            return new BillingResult(BillingStatusCode.Forbidden);
        }

        return await BuildForCompanyAsync(companyId, cancellationToken);
    }

    public async Task<BillingResult> RecordPaymentAsync(
        Guid companyId,
        RecordPaymentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!tenant.IsSuperadmin)
        {
            return new BillingResult(BillingStatusCode.Forbidden);
        }

        var company = await LoadCompanyAsync(companyId, cancellationToken);
        if (company is null)
        {
            return new BillingResult(BillingStatusCode.CompanyNotFound);
        }

        if (company.ActivatedAt is null || company.Subscription is null)
        {
            return new BillingResult(BillingStatusCode.NotActivated);
        }

        if (!BillingCalculator.TryParsePeriod(request.BillingPeriod, out var period))
        {
            return new BillingResult(BillingStatusCode.InvalidPeriod);
        }

        var now = time.GetUtcNow();
        var allowed = await PeriodWindowAsync(company.Id, company.ActivatedAt.Value, now, cancellationToken);
        if (!allowed.Contains(period))
        {
            return new BillingResult(BillingStatusCode.InvalidPeriod);
        }

        var mode = request.PaymentMode?.Trim() ?? string.Empty;
        var gst = string.IsNullOrWhiteSpace(request.InvoiceGstReference)
            ? null
            : request.InvoiceGstReference.Trim();
        if (!BillingCalculator.IsValidAmount(request.Amount)
            || !BillingCalculator.IsAllowedMode(mode)
            || gst is { Length: > 100 })
        {
            return new BillingResult(BillingStatusCode.InvalidInput);
        }

        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            SubscriptionId = company.Subscription.Id,
            Amount = request.Amount,
            PaidOn = request.PaidOn,
            PaymentMode = mode,
            InvoiceGstReference = gst,
            BillingPeriod = BillingCalculator.FormatPeriod(period),
            RecordedByUserId = tenant.UserId,
            RecordedAt = now
        });
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            ActorUserId = tenant.UserId,
            Action = AuditActions.BillingPaymentRecord,
            Details = $"{BillingCalculator.FormatPeriod(period)} {request.Amount} {mode}",
            OccurredAt = now
        });
        await db.SaveChangesAsync(cancellationToken);

        return new BillingResult(BillingStatusCode.Success, await BuildAsync(company, cancellationToken));
    }

    private async Task<BillingResult> BuildForCompanyAsync(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var company = await LoadCompanyAsync(companyId, cancellationToken);
        if (company is null)
        {
            return new BillingResult(BillingStatusCode.CompanyNotFound);
        }

        return new BillingResult(BillingStatusCode.Success, await BuildAsync(company, cancellationToken));
    }

    private async Task<Company?> LoadCompanyAsync(Guid companyId, CancellationToken cancellationToken) =>
        await db.Companies
            .Include(company => company.Subscription)
            .ThenInclude(subscription => subscription!.Plan)
            .FirstOrDefaultAsync(company => company.Id == companyId, cancellationToken);

    private async Task<CompanyBilling> BuildAsync(Company company, CancellationToken cancellationToken)
    {
        var planName = company.Subscription?.Plan?.Name ?? PlatformLimits.DefaultPlanName;
        var price = company.Subscription?.Plan?.PricePerEmployee ?? 0m;
        var grace = company.Subscription?.GracePeriodDays ?? PlatformLimits.DefaultGracePeriodDays;
        if (company.ActivatedAt is null)
        {
            return new CompanyBilling(planName, price, grace, []);
        }

        var now = time.GetUtcNow();
        var payments = await db.Payments
            .AsNoTracking()
            .Where(payment => payment.CompanyId == company.Id)
            .OrderBy(payment => payment.RecordedAt)
            .ToListAsync(cancellationToken);
        var paymentsByPeriod = payments
            .GroupBy(payment => payment.BillingPeriod)
            .ToDictionary(group => group.Key, group => group.ToList());

        var snapshots = await db.BillingPeriods
            .Where(period => period.CompanyId == company.Id)
            .ToListAsync(cancellationToken);
        var snapshotsByKey = snapshots.ToDictionary(period => period.BillingPeriod);

        var finalized = await db.PayrollRuns
            .AsNoTracking()
            .Where(run => run.CompanyId == company.Id && run.Status == PayrollRunStatus.Finalized)
            .Select(run => new { run.Id, run.Year, run.Month })
            .ToListAsync(cancellationToken);
        var periods = BillingCalculator.PeriodsThrough(
            company.ActivatedAt.Value,
            BillingCalculator.BillingEnd(
                now,
                finalized.Select(run => new PayrollPeriod(run.Year, run.Month))));
        var finalizedIds = finalized.Select(run => run.Id).ToList();
        var employeeCounts = finalizedIds.Count == 0
            ? new Dictionary<(int Year, int Month), int>()
            : (await db.PayrollEmployees
                .AsNoTracking()
                .Where(row => finalizedIds.Contains(row.PayrollRunId))
                .Select(row => new { row.PayrollRunId, row.EmployeeId })
                .ToListAsync(cancellationToken))
            .Join(
                finalized,
                row => row.PayrollRunId,
                run => run.Id,
                (row, run) => new { run.Year, run.Month, row.EmployeeId })
            .GroupBy(row => (row.Year, row.Month))
            .ToDictionary(group => group.Key, group => group.Select(row => row.EmployeeId).Distinct().Count());

        var employees = await db.Employees
            .AsNoTracking()
            .Where(employee => employee.CompanyId == company.Id)
            .Select(employee => new { employee.Status, employee.JoiningDate, employee.ExitDate })
            .ToListAsync(cancellationToken);

        var summaries = new List<BillingPeriodSummary>(periods.Count);
        var created = false;
        foreach (var period in periods)
        {
            var key = BillingCalculator.FormatPeriod(period);
            var closed = BillingCalculator.IsClosed(period, now);
            var estimated = BillingCalculator.IsCurrent(period, now);
            var periodPayments = paymentsByPeriod.GetValueOrDefault(key) ?? [];
            var items = periodPayments
                .Select(payment => new BillingPaymentItem(
                    payment.Id,
                    payment.Amount,
                    payment.PaidOn,
                    payment.PaymentMode,
                    payment.InvoiceGstReference,
                    payment.RecordedAt))
                .ToList();
            var paid = items.Sum(item => item.Amount);

            decimal periodPrice;
            int billableCount;
            BillableSource source;
            decimal amountDue;
            bool prorated;
            DateTimeOffset dueDate;

            if (closed && snapshotsByKey.TryGetValue(key, out var snapshot))
            {
                periodPrice = snapshot.PricePerEmployee;
                billableCount = snapshot.BillableEmployees;
                source = snapshot.BillableSource;
                amountDue = snapshot.AmountDue;
                prorated = snapshot.Prorated;
                dueDate = snapshot.DueDate;
            }
            else
            {
                var hasFinalized = finalized.Any(run => run.Year == period.Year && run.Month == period.Month);
                employeeCounts.TryGetValue((period.Year, period.Month), out var finalizedCount);
                var eligible = employees.Count(employee =>
                    PayrollEligibility.IsEligible(employee.Status, employee.JoiningDate, employee.ExitDate, period));
                var billable = BillingCalculator.BillableEmployees(hasFinalized, finalizedCount, eligible);
                periodPrice = price;
                billableCount = billable.Count;
                source = billable.Source;
                amountDue = BillingCalculator.AmountDue(periodPrice, billableCount, period, company.ActivatedAt);
                prorated = BillingCalculator.IsProrated(period, company.ActivatedAt);
                dueDate = BillingCalculator.DueDate(period);

                if (closed && company.Subscription is not null)
                {
                    var createdSnapshot = new BillingPeriodSnapshot
                    {
                        Id = Guid.NewGuid(),
                        CompanyId = company.Id,
                        SubscriptionId = company.Subscription.Id,
                        BillingPeriod = key,
                        PricePerEmployee = periodPrice,
                        BillableEmployees = billableCount,
                        BillableSource = source,
                        AmountDue = amountDue,
                        Prorated = prorated,
                        DueDate = dueDate
                    };
                    db.BillingPeriods.Add(createdSnapshot);
                    snapshotsByKey[key] = createdSnapshot;
                    created = true;
                }
            }

            var remaining = amountDue - paid;
            summaries.Add(new BillingPeriodSummary(
                key,
                period.Year,
                period.Month,
                billableCount,
                source,
                periodPrice,
                amountDue,
                prorated,
                estimated,
                dueDate,
                BillingCalculator.IsOverdue(remaining, dueDate, now),
                BillingCalculator.IsPastGrace(remaining, dueDate, grace, now),
                paid,
                remaining,
                items));
        }

        if (created)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return new CompanyBilling(planName, price, grace, summaries);
    }

    private async Task<IReadOnlyList<PayrollPeriod>> PeriodWindowAsync(
        Guid companyId,
        DateTimeOffset activatedAt,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var finalized = await db.PayrollRuns
            .AsNoTracking()
            .Where(run => run.CompanyId == companyId && run.Status == PayrollRunStatus.Finalized)
            .Select(run => new { run.Year, run.Month })
            .ToListAsync(cancellationToken);
        return BillingCalculator.PeriodsThrough(
            activatedAt,
            BillingCalculator.BillingEnd(
                now,
                finalized.Select(run => new PayrollPeriod(run.Year, run.Month))));
    }
}
