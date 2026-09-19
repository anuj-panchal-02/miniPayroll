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
    IReadOnlyList<BillingPaymentItem> Payments,
    string? PaymentLinkUrl = null);

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
    private readonly SubscriptionLifecycleService? subscriptions;

    public BillingService(MiniPayrollDbContext db, ITenantContext tenant)
        : this(db, tenant, TimeProvider.System)
    {
    }

    public BillingService(MiniPayrollDbContext db, ITenantContext tenant, TimeProvider time)
        : this(db, tenant, time, subscriptions: null)
    {
    }

    public BillingService(
        MiniPayrollDbContext db,
        ITenantContext tenant,
        TimeProvider time,
        SubscriptionLifecycleService? subscriptions)
    {
        this.db = db;
        this.tenant = tenant;
        this.time = time;
        this.subscriptions = subscriptions;
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

        await ApplyMatchingInvoiceAsync(company, period, request.Amount, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        var nextPeriod = period.Month == 12
            ? new PayrollPeriod(period.Year + 1, 1)
            : new PayrollPeriod(period.Year, period.Month + 1);
        var hold = await GetPriorPeriodHoldAsync(company.Id, nextPeriod, cancellationToken);
        if (!hold.IsHeld)
        {
            await (subscriptions ?? new SubscriptionLifecycleService(db, tenant, time))
                .ActivateFromTrustedPaymentAsync(company.Id, tenant.UserId, cancellationToken);
        }

        return new BillingResult(BillingStatusCode.Success, await BuildAsync(company, cancellationToken));
    }

    public async Task<PriorPeriodHold> GetPriorPeriodHoldAsync(
        Guid companyId,
        PayrollPeriod opening,
        CancellationToken cancellationToken = default)
    {
        var company = await LoadCompanyAsync(companyId, cancellationToken);
        if (company is null)
        {
            return new PriorPeriodHold(false, null);
        }

        var previous = BillingCalculator.Previous(opening);
        if (!previous.IsValid)
        {
            return PriorPeriodBillingHold.Evaluate(opening, company.ActivatedAt, 0m, 0m, false);
        }

        var key = BillingCalculator.FormatPeriod(previous);
        var recordedPaid = await db.Payments
            .Where(payment => payment.CompanyId == companyId && payment.BillingPeriod == key)
            .SumAsync(payment => (decimal?)payment.Amount, cancellationToken) ?? 0m;
        var invoicePaid = await HasPaidInvoiceAsync(companyId, previous, cancellationToken);
        var amountDue = await AmountDueForAsync(company, previous, cancellationToken);
        return PriorPeriodBillingHold.Evaluate(
            opening,
            company.ActivatedAt,
            amountDue,
            recordedPaid,
            invoicePaid);
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

        var openLinks = await db.PaymentIntents
            .AsNoTracking()
            .Where(intent => intent.CompanyId == company.Id
                && intent.Status == PaymentIntentStatus.Created
                && intent.CheckoutUrl != null
                && intent.ProviderPaymentLinkId != null)
            .OrderByDescending(intent => intent.CreatedAt)
            .ToListAsync(cancellationToken);
        if (openLinks.Count > 0)
        {
            summaries = summaries
                .Select(summary =>
                {
                    var prefix = $"plink:{company.Id:D}:{summary.BillingPeriod}";
                    var link = openLinks.FirstOrDefault(intent =>
                        intent.IdempotencyKey == prefix
                        || intent.IdempotencyKey.StartsWith(prefix + ":", StringComparison.Ordinal));
                    return link is null ? summary : summary with { PaymentLinkUrl = link.CheckoutUrl };
                })
                .ToList();
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

    private async Task ApplyMatchingInvoiceAsync(
        Company company,
        PayrollPeriod period,
        decimal amount,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var invoice = await MatchingInvoiceAsync(company.Id, period, cancellationToken);
        if (invoice is null
            || !InvoiceStatusTransitions.CanExecute(invoice.Status, InvoiceCommand.ApplyPayment))
        {
            return;
        }

        var remaining = invoice.Total - invoice.AmountPaid;
        var apply = remaining < amount ? remaining : amount;
        if (apply <= 0)
        {
            return;
        }

        InvoiceLifecycle.ApplyPayment(invoice, apply, now);
    }

    private async Task<Invoice?> MatchingInvoiceAsync(
        Guid companyId,
        PayrollPeriod period,
        CancellationToken cancellationToken)
    {
        var start = new DateTimeOffset(period.Year, period.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var end = period.Month == 12
            ? new DateTimeOffset(period.Year + 1, 1, 1, 0, 0, 0, TimeSpan.Zero)
            : new DateTimeOffset(period.Year, period.Month + 1, 1, 0, 0, 0, TimeSpan.Zero);
        return await db.Invoices
            .Where(invoice => invoice.CompanyId == companyId
                && invoice.Status != InvoiceStatus.Void
                && invoice.Status != InvoiceStatus.Refunded
                && invoice.PeriodStart >= start
                && invoice.PeriodStart < end)
            .OrderByDescending(invoice => invoice.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<bool> HasPaidInvoiceAsync(
        Guid companyId,
        PayrollPeriod period,
        CancellationToken cancellationToken)
    {
        var start = new DateTimeOffset(period.Year, period.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var end = period.Month == 12
            ? new DateTimeOffset(period.Year + 1, 1, 1, 0, 0, 0, TimeSpan.Zero)
            : new DateTimeOffset(period.Year, period.Month + 1, 1, 0, 0, 0, TimeSpan.Zero);
        return await db.Invoices.AnyAsync(
            invoice => invoice.CompanyId == companyId
                && invoice.Status == InvoiceStatus.Paid
                && invoice.PeriodStart >= start
                && invoice.PeriodStart < end,
            cancellationToken);
    }

    private async Task<decimal> AmountDueForAsync(
        Company company,
        PayrollPeriod period,
        CancellationToken cancellationToken)
    {
        var key = BillingCalculator.FormatPeriod(period);
        var snapshot = await db.BillingPeriods
            .AsNoTracking()
            .FirstOrDefaultAsync(
                item => item.CompanyId == company.Id && item.BillingPeriod == key,
                cancellationToken);
        if (snapshot is not null)
        {
            return snapshot.AmountDue;
        }

        var price = company.Subscription?.Plan?.PricePerEmployee ?? 0m;
        var finalized = await db.PayrollRuns
            .AsNoTracking()
            .Where(run => run.CompanyId == company.Id
                && run.Year == period.Year
                && run.Month == period.Month
                && run.Status == PayrollRunStatus.Finalized)
            .Select(run => run.Id)
            .ToListAsync(cancellationToken);
        var finalizedCount = finalized.Count == 0
            ? 0
            : await db.PayrollEmployees
                .AsNoTracking()
                .Where(row => finalized.Contains(row.PayrollRunId))
                .Select(row => row.EmployeeId)
                .Distinct()
                .CountAsync(cancellationToken);
        var employees = await db.Employees
            .AsNoTracking()
            .Where(employee => employee.CompanyId == company.Id)
            .Select(employee => new { employee.Status, employee.JoiningDate, employee.ExitDate })
            .ToListAsync(cancellationToken);
        var eligible = employees.Count(employee =>
            PayrollEligibility.IsEligible(employee.Status, employee.JoiningDate, employee.ExitDate, period));
        var billable = BillingCalculator.BillableEmployees(finalized.Count > 0, finalizedCount, eligible);
        return BillingCalculator.AmountDue(price, billable.Count, period, company.ActivatedAt);
    }
}
