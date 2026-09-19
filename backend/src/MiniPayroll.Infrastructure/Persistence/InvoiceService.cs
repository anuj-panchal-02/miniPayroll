using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Billing;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Payroll;
using MiniPayroll.Domain.Subscriptions;
using MiniPayroll.Domain.Tenancy;

namespace MiniPayroll.Infrastructure.Persistence;

public enum InvoiceCommandStatus
{
    Success,
    Forbidden,
    CompanyNotFound,
    NotFound,
    InvalidInput,
    InvalidTransition,
    DuplicatePeriod,
    Overpay
}

public sealed record InvoiceLineResponse(
    Guid Id,
    string Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal Amount);

public sealed record InvoiceResponse(
    Guid Id,
    Guid CompanyId,
    Guid SubscriptionId,
    string? InvoiceNumber,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    decimal Subtotal,
    decimal Tax,
    decimal Total,
    decimal AmountPaid,
    string Currency,
    string Status,
    DateTimeOffset? IssuedAt,
    DateTimeOffset? DueAt,
    DateTimeOffset? PaidAt,
    string? ExternalInvoiceId,
    DateTimeOffset CreatedAt,
    IReadOnlyList<InvoiceLineResponse> Lines);

public sealed record InvoiceCommandResult(
    InvoiceCommandStatus Status,
    InvoiceResponse? Invoice = null);

public sealed record InvoiceListResult(
    InvoiceCommandStatus Status,
    IReadOnlyList<InvoiceResponse>? Invoices = null);

public sealed class InvoiceService(
    MiniPayrollDbContext db,
    ITenantContext tenant,
    TimeProvider? time = null)
{
    public async Task<InvoiceListResult> ListAsync(
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        if (!CanRead(companyId))
        {
            return new InvoiceListResult(InvoiceCommandStatus.Forbidden);
        }

        if (!await CompanyExistsAsync(companyId, cancellationToken))
        {
            return new InvoiceListResult(InvoiceCommandStatus.CompanyNotFound);
        }

        var invoices = await Query(companyId)
            .AsNoTracking()
            .Include(invoice => invoice.Lines)
            .OrderByDescending(invoice => invoice.PeriodStart)
            .ThenByDescending(invoice => invoice.CreatedAt)
            .ToListAsync(cancellationToken);

        return new InvoiceListResult(InvoiceCommandStatus.Success, invoices.Select(ToResponse).ToList());
    }

    public async Task<InvoiceCommandResult> GetAsync(
        Guid companyId,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        if (!CanRead(companyId))
        {
            return new InvoiceCommandResult(InvoiceCommandStatus.Forbidden);
        }

        var invoice = await Query(companyId)
            .AsNoTracking()
            .Include(item => item.Lines)
            .FirstOrDefaultAsync(item => item.Id == invoiceId, cancellationToken);
        return invoice is null
            ? new InvoiceCommandResult(InvoiceCommandStatus.NotFound)
            : new InvoiceCommandResult(InvoiceCommandStatus.Success, ToResponse(invoice));
    }

    public async Task<InvoiceListResult> ListOwnAsync(CancellationToken cancellationToken = default)
    {
        if (tenant.IsSuperadmin || tenant.CompanyId is not { } companyId)
        {
            return new InvoiceListResult(InvoiceCommandStatus.Forbidden);
        }

        return await ListAsync(companyId, cancellationToken);
    }

    public async Task<InvoiceCommandResult> GetOwnAsync(
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        if (tenant.IsSuperadmin || tenant.CompanyId is not { } companyId)
        {
            return new InvoiceCommandResult(InvoiceCommandStatus.Forbidden);
        }

        return await GetAsync(companyId, invoiceId, cancellationToken);
    }

    public async Task<InvoiceCommandResult> CreateAsync(
        Guid companyId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        decimal? quantity,
        CancellationToken cancellationToken = default)
    {
        if (!tenant.IsSuperadmin)
        {
            return new InvoiceCommandResult(InvoiceCommandStatus.Forbidden);
        }

        var company = await LoadCompanyAsync(companyId, cancellationToken);
        if (company?.Subscription is null)
        {
            return new InvoiceCommandResult(InvoiceCommandStatus.CompanyNotFound);
        }

        var seats = quantity is > 0
            ? quantity.Value
            : await SnapshotQuantityAsync(companyId, periodStart, cancellationToken);
        if (seats <= 0)
        {
            return new InvoiceCommandResult(InvoiceCommandStatus.InvalidInput);
        }

        if (await HasOpenPeriodAsync(companyId, periodStart, periodEnd, excludeId: null, cancellationToken))
        {
            return new InvoiceCommandResult(InvoiceCommandStatus.DuplicatePeriod);
        }

        var price = PlanPricing.Current(
            company.Subscription.Plan.Prices,
            company.Subscription.BillingCycle,
            periodStart);
        var unitPrice = price?.Amount ?? company.Subscription.Plan.PricePerEmployee;
        var currency = price?.Currency ?? PlatformLimits.CurrencyCode;
        var now = Now();
        var created = InvoiceLifecycle.Create(
            companyId,
            company.Subscription.Id,
            periodStart,
            periodEnd,
            seats,
            unitPrice,
            currency,
            company.Subscription.Plan.Name,
            now);
        if (created.Status != InvoiceLifecycleStatus.Success || created.Invoice is null)
        {
            return new InvoiceCommandResult(InvoiceCommandStatus.InvalidInput);
        }

        db.Invoices.Add(created.Invoice);
        db.AuditLogs.Add(Audit(companyId, AuditActions.InvoiceCreate, now));
        await db.SaveChangesAsync(cancellationToken);
        return new InvoiceCommandResult(InvoiceCommandStatus.Success, ToResponse(created.Invoice));
    }

    public async Task<InvoiceCommandResult> IssueAsync(
        Guid companyId,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        if (!tenant.IsSuperadmin)
        {
            return new InvoiceCommandResult(InvoiceCommandStatus.Forbidden);
        }

        var invoice = await Query(companyId)
            .Include(item => item.Lines)
            .FirstOrDefaultAsync(item => item.Id == invoiceId, cancellationToken);
        if (invoice is null)
        {
            return new InvoiceCommandResult(InvoiceCommandStatus.NotFound);
        }

        var year = Now().Year;
        var existing = await db.Invoices
            .IgnoreQueryFilters()
            .Where(item => item.InvoiceNumber != null)
            .Select(item => item.InvoiceNumber!)
            .ToListAsync(cancellationToken);
        var number = InvoiceNumbering.Format(year, InvoiceNumbering.NextSequence(existing, year));
        var issued = InvoiceLifecycle.Issue(invoice, number, Now());
        if (issued.Status != InvoiceLifecycleStatus.Success)
        {
            return new InvoiceCommandResult(Map(issued.Status));
        }

        db.AuditLogs.Add(Audit(companyId, AuditActions.InvoiceIssue, Now()));
        await db.SaveChangesAsync(cancellationToken);
        return new InvoiceCommandResult(InvoiceCommandStatus.Success, ToResponse(invoice));
    }

    public async Task<InvoiceCommandResult> EnsureIssuedForPeriodAsync(
        Guid companyId,
        PayrollPeriod period,
        int billableEmployees,
        decimal amountDue,
        decimal recordedPaid,
        CancellationToken cancellationToken = default)
    {
        if (!tenant.IsSuperadmin)
        {
            return new InvoiceCommandResult(InvoiceCommandStatus.Forbidden);
        }

        if (!period.IsValid || billableEmployees <= 0 || amountDue <= 0)
        {
            return new InvoiceCommandResult(InvoiceCommandStatus.InvalidInput);
        }

        var company = await LoadCompanyAsync(companyId, cancellationToken);
        if (company?.Subscription is null)
        {
            return new InvoiceCommandResult(InvoiceCommandStatus.CompanyNotFound);
        }

        var start = new DateTimeOffset(period.Year, period.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var endExclusive = period.Month == 12
            ? new DateTimeOffset(period.Year + 1, 1, 1, 0, 0, 0, TimeSpan.Zero)
            : new DateTimeOffset(period.Year, period.Month + 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = endExclusive.AddSeconds(-1);

        var existing = await Query(companyId)
            .Include(item => item.Lines)
            .Where(invoice => invoice.PeriodStart >= start
                && invoice.PeriodStart < endExclusive
                && invoice.Status != InvoiceStatus.Void
                && invoice.Status != InvoiceStatus.Refunded)
            .OrderByDescending(invoice => invoice.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        Invoice invoice;
        if (existing is not null)
        {
            if (existing.Status == InvoiceStatus.Paid)
            {
                return new InvoiceCommandResult(InvoiceCommandStatus.InvalidInput, ToResponse(existing));
            }

            invoice = existing;
            if (invoice.Status == InvoiceStatus.Draft)
            {
                var year = Now().Year;
                var numbers = await db.Invoices
                    .IgnoreQueryFilters()
                    .Where(item => item.InvoiceNumber != null)
                    .Select(item => item.InvoiceNumber!)
                    .ToListAsync(cancellationToken);
                var number = InvoiceNumbering.Format(year, InvoiceNumbering.NextSequence(numbers, year));
                var issued = InvoiceLifecycle.Issue(invoice, number, Now());
                if (issued.Status != InvoiceLifecycleStatus.Success)
                {
                    return new InvoiceCommandResult(Map(issued.Status));
                }

                db.AuditLogs.Add(Audit(companyId, AuditActions.InvoiceIssue, Now()));
            }
        }
        else
        {
            var created = await CreateAsync(companyId, start, end, billableEmployees, cancellationToken);
            if (created.Status != InvoiceCommandStatus.Success || created.Invoice is null)
            {
                return created;
            }

            var issued = await IssueAsync(companyId, created.Invoice.Id, cancellationToken);
            if (issued.Status != InvoiceCommandStatus.Success || issued.Invoice is null)
            {
                return issued;
            }

            invoice = await Query(companyId)
                .Include(item => item.Lines)
                .FirstAsync(item => item.Id == created.Invoice.Id, cancellationToken);
        }

        var syncPaid = Math.Min(recordedPaid, invoice.Total) - invoice.AmountPaid;
        if (syncPaid > 0)
        {
            var applied = InvoiceLifecycle.ApplyPayment(invoice, syncPaid, Now());
            if (applied.Status != InvoiceLifecycleStatus.Success)
            {
                return new InvoiceCommandResult(Map(applied.Status));
            }
        }

        if (InvoiceStatusTransitions.CanExecute(invoice.Status, InvoiceCommand.MarkPaymentPending))
        {
            InvoiceLifecycle.MarkPaymentPending(invoice);
            db.AuditLogs.Add(Audit(companyId, AuditActions.InvoicePaymentPending, Now()));
        }

        await db.SaveChangesAsync(cancellationToken);
        return new InvoiceCommandResult(InvoiceCommandStatus.Success, ToResponse(invoice));
    }

    public Task<InvoiceCommandResult> MarkPaymentPendingAsync(
        Guid companyId,
        Guid invoiceId,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            companyId,
            invoiceId,
            AuditActions.InvoicePaymentPending,
            invoice => InvoiceLifecycle.MarkPaymentPending(invoice),
            cancellationToken);

    public async Task<InvoiceCommandResult> ApplyPaymentAsync(
        Guid companyId,
        Guid invoiceId,
        decimal amount,
        CancellationToken cancellationToken = default)
    {
        var result = await MutateAsync(
            companyId,
            invoiceId,
            AuditActions.InvoicePayment,
            invoice => InvoiceLifecycle.ApplyPayment(invoice, amount, Now()),
            cancellationToken);
        if (result.Status == InvoiceCommandStatus.Success)
        {
            await SyncPaidInvoiceAsync(companyId, invoiceId, cancellationToken);
        }

        return result.Status == InvoiceCommandStatus.Success
            ? await GetAsync(companyId, invoiceId, cancellationToken)
            : result;
    }

    public Task<InvoiceCommandResult> ApplyTrustedPaymentAsync(
        Guid companyId,
        Guid invoiceId,
        decimal amount,
        CancellationToken cancellationToken = default) =>
        ApplyTrustedPaymentAsync(companyId, invoiceId, amount, persist: true, cancellationToken);

    public async Task<InvoiceCommandResult> ApplyTrustedPaymentAsync(
        Guid companyId,
        Guid invoiceId,
        decimal amount,
        bool persist,
        CancellationToken cancellationToken = default)
    {
        var invoice = await LoadTrustedInvoiceAsync(companyId, invoiceId, cancellationToken);
        if (invoice is null)
        {
            return new InvoiceCommandResult(InvoiceCommandStatus.NotFound);
        }

        if (invoice.Status == InvoiceStatus.Paid)
        {
            return new InvoiceCommandResult(InvoiceCommandStatus.Success, ToResponse(invoice));
        }

        var applied = InvoiceLifecycle.ApplyPayment(invoice, amount, Now());
        if (applied.Status != InvoiceLifecycleStatus.Success)
        {
            return new InvoiceCommandResult(Map(applied.Status));
        }

        db.AuditLogs.Add(Audit(companyId, AuditActions.InvoicePayment, Now()));
        if (invoice.Status == InvoiceStatus.Paid)
        {
            await BillingPeriodPaymentSync.EnsureCoveringPaymentAsync(
                db, invoice, tenant.UserId, Now(), cancellationToken);
        }
        if (persist)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return new InvoiceCommandResult(InvoiceCommandStatus.Success, ToResponse(invoice));
    }

    public async Task<InvoiceCommandResult> MarkTrustedFailedAsync(
        Guid companyId,
        Guid invoiceId,
        bool persist,
        CancellationToken cancellationToken = default)
    {
        var invoice = await LoadTrustedInvoiceAsync(companyId, invoiceId, cancellationToken);
        if (invoice is null)
        {
            return new InvoiceCommandResult(InvoiceCommandStatus.NotFound);
        }

        if (invoice.Status is InvoiceStatus.Failed or InvoiceStatus.Paid or InvoiceStatus.Refunded)
        {
            return new InvoiceCommandResult(InvoiceCommandStatus.Success, ToResponse(invoice));
        }

        var applied = InvoiceLifecycle.MarkFailed(invoice);
        if (applied.Status != InvoiceLifecycleStatus.Success)
        {
            return new InvoiceCommandResult(Map(applied.Status));
        }

        db.AuditLogs.Add(Audit(companyId, AuditActions.InvoiceFail, Now()));
        if (persist)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return new InvoiceCommandResult(InvoiceCommandStatus.Success, ToResponse(invoice));
    }

    public async Task<InvoiceCommandResult> MarkTrustedRefundedAsync(
        Guid companyId,
        Guid invoiceId,
        bool persist,
        CancellationToken cancellationToken = default)
    {
        var invoice = await LoadTrustedInvoiceAsync(companyId, invoiceId, cancellationToken);
        if (invoice is null)
        {
            return new InvoiceCommandResult(InvoiceCommandStatus.NotFound);
        }

        if (invoice.Status == InvoiceStatus.Refunded)
        {
            return new InvoiceCommandResult(InvoiceCommandStatus.Success, ToResponse(invoice));
        }

        var applied = InvoiceLifecycle.MarkRefunded(invoice);
        if (applied.Status != InvoiceLifecycleStatus.Success)
        {
            return new InvoiceCommandResult(InvoiceCommandStatus.Success, ToResponse(invoice));
        }

        db.AuditLogs.Add(Audit(companyId, AuditActions.InvoiceRefund, Now()));
        if (persist)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return new InvoiceCommandResult(InvoiceCommandStatus.Success, ToResponse(invoice));
    }

    private Task<Invoice?> LoadTrustedInvoiceAsync(
        Guid companyId,
        Guid invoiceId,
        CancellationToken cancellationToken) =>
        db.Invoices
            .IgnoreQueryFilters()
            .Include(item => item.Lines)
            .FirstOrDefaultAsync(
                item => item.Id == invoiceId && item.CompanyId == companyId,
                cancellationToken);

    public Task<InvoiceCommandResult> MarkFailedAsync(
        Guid companyId,
        Guid invoiceId,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            companyId,
            invoiceId,
            AuditActions.InvoiceFail,
            invoice => InvoiceLifecycle.MarkFailed(invoice),
            cancellationToken);

    public Task<InvoiceCommandResult> VoidAsync(
        Guid companyId,
        Guid invoiceId,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            companyId,
            invoiceId,
            AuditActions.InvoiceVoid,
            invoice => InvoiceLifecycle.Void(invoice),
            cancellationToken);

    public Task<InvoiceCommandResult> MarkRefundedAsync(
        Guid companyId,
        Guid invoiceId,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            companyId,
            invoiceId,
            AuditActions.InvoiceRefund,
            invoice => InvoiceLifecycle.MarkRefunded(invoice),
            cancellationToken);

    private async Task<InvoiceCommandResult> MutateAsync(
        Guid companyId,
        Guid invoiceId,
        string auditAction,
        Func<Invoice, InvoiceLifecycleResult> apply,
        CancellationToken cancellationToken)
    {
        if (!tenant.IsSuperadmin)
        {
            return new InvoiceCommandResult(InvoiceCommandStatus.Forbidden);
        }

        var invoice = await Query(companyId)
            .Include(item => item.Lines)
            .FirstOrDefaultAsync(item => item.Id == invoiceId, cancellationToken);
        if (invoice is null)
        {
            return new InvoiceCommandResult(InvoiceCommandStatus.NotFound);
        }

        var applied = apply(invoice);
        if (applied.Status != InvoiceLifecycleStatus.Success)
        {
            return new InvoiceCommandResult(Map(applied.Status));
        }

        db.AuditLogs.Add(Audit(companyId, auditAction, Now()));
        await db.SaveChangesAsync(cancellationToken);
        return new InvoiceCommandResult(InvoiceCommandStatus.Success, ToResponse(invoice));
    }

    private async Task SyncPaidInvoiceAsync(
        Guid companyId,
        Guid invoiceId,
        CancellationToken cancellationToken)
    {
        var invoice = await Query(companyId)
            .FirstOrDefaultAsync(item => item.Id == invoiceId, cancellationToken);
        if (invoice is null || invoice.Status != InvoiceStatus.Paid)
        {
            return;
        }

        await BillingPeriodPaymentSync.EnsureCoveringPaymentAsync(
            db, invoice, tenant.UserId, Now(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<Invoice> Query(Guid companyId)
    {
        var invoices = db.Invoices.Where(invoice => invoice.CompanyId == companyId);
        return tenant.IsSuperadmin ? invoices.IgnoreQueryFilters().Where(invoice => invoice.CompanyId == companyId) : invoices;
    }

    private bool CanRead(Guid companyId) =>
        tenant.IsSuperadmin || tenant.CompanyId == companyId;

    private async Task<bool> CompanyExistsAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var companies = tenant.IsSuperadmin ? db.Companies.IgnoreQueryFilters() : db.Companies;
        return await companies.AnyAsync(company => company.Id == companyId, cancellationToken);
    }

    private async Task<Company?> LoadCompanyAsync(Guid companyId, CancellationToken cancellationToken)
    {
        var companies = db.Companies
            .Include(company => company.Subscription)
            .ThenInclude(subscription => subscription!.Plan)
            .ThenInclude(plan => plan.Prices)
            .AsQueryable();
        if (tenant.IsSuperadmin)
        {
            companies = companies.IgnoreQueryFilters();
        }

        return await companies.FirstOrDefaultAsync(company => company.Id == companyId, cancellationToken);
    }

    private async Task<bool> HasOpenPeriodAsync(
        Guid companyId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var invoices = tenant.IsSuperadmin
            ? db.Invoices.IgnoreQueryFilters()
            : db.Invoices;
        return await invoices.AnyAsync(
            invoice => invoice.CompanyId == companyId
                && invoice.PeriodStart == periodStart
                && invoice.PeriodEnd == periodEnd
                && invoice.Status != InvoiceStatus.Void
                && (excludeId == null || invoice.Id != excludeId),
            cancellationToken);
    }

    private async Task<decimal> SnapshotQuantityAsync(
        Guid companyId,
        DateTimeOffset periodStart,
        CancellationToken cancellationToken)
    {
        var key = $"{periodStart.UtcDateTime.Year}-{periodStart.UtcDateTime.Month:D2}";
        var snapshots = tenant.IsSuperadmin
            ? db.BillingPeriods.IgnoreQueryFilters()
            : db.BillingPeriods;
        var snapshot = await snapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(
                period => period.CompanyId == companyId && period.BillingPeriod == key,
                cancellationToken);
        return snapshot?.BillableEmployees ?? 0;
    }

    private DateTimeOffset Now() => (time ?? TimeProvider.System).GetUtcNow();

    private AuditLog Audit(Guid companyId, string action, DateTimeOffset now) =>
        new()
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            ActorUserId = tenant.UserId,
            Action = action,
            OccurredAt = now
        };

    private static InvoiceCommandStatus Map(InvoiceLifecycleStatus status) =>
        status switch
        {
            InvoiceLifecycleStatus.InvalidTransition => InvoiceCommandStatus.InvalidTransition,
            InvoiceLifecycleStatus.Overpay => InvoiceCommandStatus.Overpay,
            _ => InvoiceCommandStatus.InvalidInput
        };

    private static InvoiceResponse ToResponse(Invoice invoice) =>
        new(
            invoice.Id,
            invoice.CompanyId,
            invoice.SubscriptionId,
            invoice.InvoiceNumber,
            invoice.PeriodStart,
            invoice.PeriodEnd,
            invoice.Subtotal,
            invoice.Tax,
            invoice.Total,
            invoice.AmountPaid,
            invoice.Currency,
            invoice.Status.ToString(),
            invoice.IssuedAt,
            invoice.DueAt,
            invoice.PaidAt,
            invoice.ExternalInvoiceId,
            invoice.CreatedAt,
            invoice.Lines
                .OrderBy(line => line.Description)
                .Select(line => new InvoiceLineResponse(
                    line.Id,
                    line.Description,
                    line.Quantity,
                    line.UnitPrice,
                    line.Amount))
                .ToList());
}
