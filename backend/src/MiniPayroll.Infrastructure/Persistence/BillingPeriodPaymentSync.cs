using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Billing;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Infrastructure.Persistence;

public static class BillingPeriodPaymentSync
{
    public static async Task EnsureCoveringPaymentAsync(
        MiniPayrollDbContext db,
        Invoice invoice,
        Guid actorUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        if (invoice.Status != InvoiceStatus.Paid)
        {
            return;
        }

        var key = BillingCalculator.FormatPeriod(
            new MiniPayroll.Domain.Payroll.PayrollPeriod(
                invoice.PeriodStart.UtcDateTime.Year,
                invoice.PeriodStart.UtcDateTime.Month));
        var local = db.Payments.Local
            .Where(payment => payment.CompanyId == invoice.CompanyId && payment.BillingPeriod == key)
            .ToList();
        var localIds = local.Select(payment => payment.Id).ToList();
        var stored = await db.Payments
            .Where(payment => payment.CompanyId == invoice.CompanyId
                && payment.BillingPeriod == key
                && !localIds.Contains(payment.Id))
            .SumAsync(payment => (decimal?)payment.Amount, cancellationToken) ?? 0m;
        var paid = local.Sum(payment => payment.Amount) + stored;
        var gap = invoice.Total - paid;
        if (gap <= 0)
        {
            return;
        }

        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            CompanyId = invoice.CompanyId,
            SubscriptionId = invoice.SubscriptionId,
            Amount = gap,
            PaidOn = invoice.PaidAt ?? now,
            PaymentMode = BillingCalculator.ModeUpi,
            InvoiceGstReference = invoice.InvoiceNumber,
            BillingPeriod = key,
            RecordedByUserId = actorUserId,
            RecordedAt = now
        });
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            CompanyId = invoice.CompanyId,
            ActorUserId = actorUserId,
            Action = AuditActions.BillingPaymentRecord,
            Details = $"{key} {gap} {BillingCalculator.ModeUpi}",
            OccurredAt = now
        });
    }
}
