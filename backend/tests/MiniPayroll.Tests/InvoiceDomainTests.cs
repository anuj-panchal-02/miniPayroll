using MiniPayroll.Domain.Billing;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Tests;

public sealed class InvoiceDomainTests
{
    [Fact]
    public void Create_builds_a_draft_with_frozen_line_totals_and_zero_tax()
    {
        var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.Zero);
        var result = InvoiceLifecycle.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            start,
            end,
            quantity: 3,
            unitPrice: 499m,
            "INR",
            "Starter",
            start);

        Assert.Equal(InvoiceLifecycleStatus.Success, result.Status);
        var invoice = result.Invoice!;
        var line = Assert.Single(invoice.Lines);
        Assert.Equal(InvoiceStatus.Draft, invoice.Status);
        Assert.Equal(3m, line.Quantity);
        Assert.Equal(499m, line.UnitPrice);
        Assert.Equal(1497m, line.Amount);
        Assert.Equal(1497m, invoice.Subtotal);
        Assert.Equal(0m, invoice.Tax);
        Assert.Equal(1497m, invoice.Total);
        Assert.Equal(InvoiceTotals.Total(invoice.Subtotal, invoice.Tax), invoice.Total);
        Assert.Contains("Starter", line.Description, StringComparison.Ordinal);
        Assert.Null(invoice.ExternalInvoiceId);
    }

    [Fact]
    public void Line_amount_and_totals_round_away_from_zero()
    {
        Assert.Equal(12.35m, InvoiceTotals.LineAmount(2.5m, 4.938m));
        Assert.Equal(12.36m, InvoiceTotals.Subtotal([6.185m, 6.175m]));
        Assert.Equal(10.00m, InvoiceTotals.Total(10.00m, 0m));
        Assert.True(InvoiceTotals.IsValidMoney(10.00m));
        Assert.False(InvoiceTotals.IsValidMoney(10.001m));
    }

    [Fact]
    public void Invoice_numbers_are_year_scoped_and_unique()
    {
        Assert.Equal("INV-2026-000001", InvoiceNumbering.Format(2026, 1));
        Assert.Equal(3, InvoiceNumbering.NextSequence(["INV-2026-000001", "INV-2026-000002", "INV-2025-000099"], 2026));
        Assert.True(InvoiceNumbering.TryParse("INV-2026-000010", out var year, out var sequence));
        Assert.Equal(2026, year);
        Assert.Equal(10, sequence);
    }

    [Fact]
    public void Exhaustive_status_matrix_matches_the_command_graph()
    {
        HashSet<(InvoiceStatus From, InvoiceStatus To)> allowed =
        [
            (InvoiceStatus.Draft, InvoiceStatus.Issued),
            (InvoiceStatus.Draft, InvoiceStatus.Void),
            (InvoiceStatus.Issued, InvoiceStatus.PaymentPending),
            (InvoiceStatus.Issued, InvoiceStatus.Paid),
            (InvoiceStatus.Issued, InvoiceStatus.PartiallyPaid),
            (InvoiceStatus.Issued, InvoiceStatus.Failed),
            (InvoiceStatus.Issued, InvoiceStatus.Void),
            (InvoiceStatus.PaymentPending, InvoiceStatus.Paid),
            (InvoiceStatus.PaymentPending, InvoiceStatus.PartiallyPaid),
            (InvoiceStatus.PaymentPending, InvoiceStatus.Failed),
            (InvoiceStatus.PaymentPending, InvoiceStatus.Void),
            (InvoiceStatus.PartiallyPaid, InvoiceStatus.Paid),
            (InvoiceStatus.PartiallyPaid, InvoiceStatus.Failed),
            (InvoiceStatus.PartiallyPaid, InvoiceStatus.Void),
            (InvoiceStatus.PartiallyPaid, InvoiceStatus.Refunded),
            (InvoiceStatus.Paid, InvoiceStatus.Refunded),
            (InvoiceStatus.Failed, InvoiceStatus.Issued),
            (InvoiceStatus.Failed, InvoiceStatus.PaymentPending),
            (InvoiceStatus.Failed, InvoiceStatus.Void)
        ];

        foreach (var from in Enum.GetValues<InvoiceStatus>())
        {
            foreach (var to in Enum.GetValues<InvoiceStatus>())
            {
                var expected = from == to || allowed.Contains((from, to));
                Assert.Equal(expected, InvoiceStatusTransitions.CanTransition(from, to));
            }
        }
    }

    [Fact]
    public void Apply_payment_moves_through_partial_then_paid_and_rejects_overpay()
    {
        var now = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
        var invoice = InvoiceLifecycle.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            now,
            now.AddMonths(1).AddSeconds(-1),
            2,
            499m,
            "INR",
            "Starter",
            now).Invoice!;
        InvoiceLifecycle.Issue(invoice, "INV-2026-000001", now);

        var partial = InvoiceLifecycle.ApplyPayment(invoice, 400m, now);
        Assert.Equal(InvoiceLifecycleStatus.Success, partial.Status);
        Assert.Equal(InvoiceStatus.PartiallyPaid, invoice.Status);
        Assert.Equal(400m, invoice.AmountPaid);
        Assert.Null(invoice.PaidAt);

        Assert.Equal(InvoiceLifecycleStatus.Overpay, InvoiceLifecycle.ApplyPayment(invoice, 700m, now).Status);

        var paid = InvoiceLifecycle.ApplyPayment(invoice, 598m, now);
        Assert.Equal(InvoiceLifecycleStatus.Success, paid.Status);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(998m, invoice.AmountPaid);
        Assert.Equal(now, invoice.PaidAt);
    }

    [Fact]
    public void Void_and_refund_follow_the_named_commands()
    {
        var invoice = new Invoice { Status = InvoiceStatus.Draft };
        Assert.Equal(InvoiceLifecycleStatus.Success, InvoiceLifecycle.Void(invoice).Status);
        Assert.Equal(InvoiceStatus.Void, invoice.Status);
        Assert.Equal(InvoiceLifecycleStatus.InvalidTransition, InvoiceLifecycle.Issue(invoice, "INV-2026-000001", DateTimeOffset.UtcNow).Status);

        var paid = new Invoice { Status = InvoiceStatus.Paid };
        Assert.Equal(InvoiceLifecycleStatus.Success, InvoiceLifecycle.MarkRefunded(paid).Status);
        Assert.Equal(InvoiceStatus.Refunded, paid.Status);
        Assert.Equal(InvoiceLifecycleStatus.InvalidTransition, InvoiceLifecycle.Void(paid).Status);
    }
}
