namespace MiniPayroll.Domain.Billing;

public enum InvoiceCommand
{
    Create,
    Issue,
    MarkPaymentPending,
    ApplyPayment,
    MarkFailed,
    Void,
    MarkRefunded
}
