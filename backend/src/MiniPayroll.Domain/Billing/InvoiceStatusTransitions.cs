using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Billing;

public static class InvoiceStatusTransitions
{
    public static bool CanTransition(InvoiceStatus from, InvoiceStatus to)
    {
        if (from == to)
        {
            return true;
        }

        return (from, to) switch
        {
            (InvoiceStatus.Draft, InvoiceStatus.Issued) => true,
            (InvoiceStatus.Draft, InvoiceStatus.Void) => true,
            (InvoiceStatus.Issued, InvoiceStatus.PaymentPending) => true,
            (InvoiceStatus.Issued, InvoiceStatus.Paid) => true,
            (InvoiceStatus.Issued, InvoiceStatus.PartiallyPaid) => true,
            (InvoiceStatus.Issued, InvoiceStatus.Failed) => true,
            (InvoiceStatus.Issued, InvoiceStatus.Void) => true,
            (InvoiceStatus.PaymentPending, InvoiceStatus.Paid) => true,
            (InvoiceStatus.PaymentPending, InvoiceStatus.PartiallyPaid) => true,
            (InvoiceStatus.PaymentPending, InvoiceStatus.Failed) => true,
            (InvoiceStatus.PaymentPending, InvoiceStatus.Void) => true,
            (InvoiceStatus.PartiallyPaid, InvoiceStatus.Paid) => true,
            (InvoiceStatus.PartiallyPaid, InvoiceStatus.Failed) => true,
            (InvoiceStatus.PartiallyPaid, InvoiceStatus.Void) => true,
            (InvoiceStatus.PartiallyPaid, InvoiceStatus.Refunded) => true,
            (InvoiceStatus.Paid, InvoiceStatus.Refunded) => true,
            (InvoiceStatus.Failed, InvoiceStatus.Issued) => true,
            (InvoiceStatus.Failed, InvoiceStatus.PaymentPending) => true,
            (InvoiceStatus.Failed, InvoiceStatus.Void) => true,
            _ => false
        };
    }

    public static bool CanExecute(InvoiceStatus from, InvoiceCommand command) =>
        command switch
        {
            InvoiceCommand.Issue => from == InvoiceStatus.Draft,
            InvoiceCommand.MarkPaymentPending =>
                from is InvoiceStatus.Issued or InvoiceStatus.Failed,
            InvoiceCommand.ApplyPayment =>
                from is InvoiceStatus.Issued
                    or InvoiceStatus.PaymentPending
                    or InvoiceStatus.PartiallyPaid,
            InvoiceCommand.MarkFailed =>
                from is InvoiceStatus.Issued
                    or InvoiceStatus.PaymentPending
                    or InvoiceStatus.PartiallyPaid,
            InvoiceCommand.Void =>
                from is InvoiceStatus.Draft
                    or InvoiceStatus.Issued
                    or InvoiceStatus.PaymentPending
                    or InvoiceStatus.Failed
                    or InvoiceStatus.PartiallyPaid,
            InvoiceCommand.MarkRefunded =>
                from is InvoiceStatus.Paid or InvoiceStatus.PartiallyPaid,
            _ => false
        };
}
