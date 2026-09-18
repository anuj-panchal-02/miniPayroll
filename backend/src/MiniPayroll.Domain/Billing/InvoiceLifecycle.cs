using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Billing;

public enum InvoiceLifecycleStatus
{
    Success,
    InvalidTransition,
    InvalidInput,
    Overpay
}

public sealed record InvoiceLifecycleResult(
    InvoiceLifecycleStatus Status,
    Invoice? Invoice = null);

public static class InvoiceLifecycle
{
    public static InvoiceLifecycleResult Create(
        Guid companyId,
        Guid subscriptionId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        decimal quantity,
        decimal unitPrice,
        string currency,
        string planName,
        DateTimeOffset now)
    {
        if (periodEnd < periodStart
            || quantity <= 0
            || !InvoiceTotals.IsValidMoney(unitPrice)
            || string.IsNullOrWhiteSpace(currency))
        {
            return new InvoiceLifecycleResult(InvoiceLifecycleStatus.InvalidInput);
        }

        var amount = InvoiceTotals.LineAmount(quantity, unitPrice);
        var subtotal = InvoiceTotals.Subtotal([amount]);
        const decimal tax = 0m;
        var invoiceId = Guid.NewGuid();
        var invoice = new Invoice
        {
            Id = invoiceId,
            CompanyId = companyId,
            SubscriptionId = subscriptionId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            Subtotal = subtotal,
            Tax = tax,
            Total = InvoiceTotals.Total(subtotal, tax),
            AmountPaid = 0m,
            Currency = currency,
            Status = InvoiceStatus.Draft,
            CreatedAt = now,
            Lines =
            [
                new InvoiceLine
                {
                    Id = Guid.NewGuid(),
                    InvoiceId = invoiceId,
                    CompanyId = companyId,
                    Description = LineDescription(planName, periodStart, periodEnd),
                    Quantity = quantity,
                    UnitPrice = InvoiceTotals.Round(unitPrice),
                    Amount = amount
                }
            ]
        };

        return new InvoiceLifecycleResult(InvoiceLifecycleStatus.Success, invoice);
    }

    public static InvoiceLifecycleResult Issue(
        Invoice invoice,
        string invoiceNumber,
        DateTimeOffset now)
    {
        if (!InvoiceStatusTransitions.CanExecute(invoice.Status, InvoiceCommand.Issue))
        {
            return new InvoiceLifecycleResult(InvoiceLifecycleStatus.InvalidTransition, invoice);
        }

        if (string.IsNullOrWhiteSpace(invoiceNumber))
        {
            return new InvoiceLifecycleResult(InvoiceLifecycleStatus.InvalidInput, invoice);
        }

        invoice.Status = InvoiceStatus.Issued;
        invoice.InvoiceNumber = invoiceNumber;
        invoice.IssuedAt = now;
        invoice.DueAt = invoice.PeriodEnd;
        return new InvoiceLifecycleResult(InvoiceLifecycleStatus.Success, invoice);
    }

    public static InvoiceLifecycleResult MarkPaymentPending(Invoice invoice)
    {
        if (!InvoiceStatusTransitions.CanExecute(invoice.Status, InvoiceCommand.MarkPaymentPending))
        {
            return new InvoiceLifecycleResult(InvoiceLifecycleStatus.InvalidTransition, invoice);
        }

        invoice.Status = InvoiceStatus.PaymentPending;
        return new InvoiceLifecycleResult(InvoiceLifecycleStatus.Success, invoice);
    }

    public static InvoiceLifecycleResult ApplyPayment(Invoice invoice, decimal amount, DateTimeOffset now)
    {
        if (!InvoiceStatusTransitions.CanExecute(invoice.Status, InvoiceCommand.ApplyPayment))
        {
            return new InvoiceLifecycleResult(InvoiceLifecycleStatus.InvalidTransition, invoice);
        }

        if (amount <= 0 || !InvoiceTotals.IsValidMoney(amount))
        {
            return new InvoiceLifecycleResult(InvoiceLifecycleStatus.InvalidInput, invoice);
        }

        var nextPaid = InvoiceTotals.Round(invoice.AmountPaid + amount);
        if (nextPaid > invoice.Total)
        {
            return new InvoiceLifecycleResult(InvoiceLifecycleStatus.Overpay, invoice);
        }

        invoice.AmountPaid = nextPaid;
        if (nextPaid == invoice.Total)
        {
            invoice.Status = InvoiceStatus.Paid;
            invoice.PaidAt = now;
        }
        else
        {
            invoice.Status = InvoiceStatus.PartiallyPaid;
        }

        return new InvoiceLifecycleResult(InvoiceLifecycleStatus.Success, invoice);
    }

    public static InvoiceLifecycleResult MarkFailed(Invoice invoice)
    {
        if (!InvoiceStatusTransitions.CanExecute(invoice.Status, InvoiceCommand.MarkFailed))
        {
            return new InvoiceLifecycleResult(InvoiceLifecycleStatus.InvalidTransition, invoice);
        }

        invoice.Status = InvoiceStatus.Failed;
        return new InvoiceLifecycleResult(InvoiceLifecycleStatus.Success, invoice);
    }

    public static InvoiceLifecycleResult Void(Invoice invoice)
    {
        if (!InvoiceStatusTransitions.CanExecute(invoice.Status, InvoiceCommand.Void))
        {
            return new InvoiceLifecycleResult(InvoiceLifecycleStatus.InvalidTransition, invoice);
        }

        invoice.Status = InvoiceStatus.Void;
        return new InvoiceLifecycleResult(InvoiceLifecycleStatus.Success, invoice);
    }

    public static InvoiceLifecycleResult MarkRefunded(Invoice invoice)
    {
        if (!InvoiceStatusTransitions.CanExecute(invoice.Status, InvoiceCommand.MarkRefunded))
        {
            return new InvoiceLifecycleResult(InvoiceLifecycleStatus.InvalidTransition, invoice);
        }

        invoice.Status = InvoiceStatus.Refunded;
        return new InvoiceLifecycleResult(InvoiceLifecycleStatus.Success, invoice);
    }

    public static string LineDescription(string planName, DateTimeOffset periodStart, DateTimeOffset periodEnd)
    {
        var name = string.IsNullOrWhiteSpace(planName) ? "Subscription" : planName.Trim();
        return $"{name} · {periodStart:yyyy-MM-dd} – {periodEnd:yyyy-MM-dd}";
    }
}
