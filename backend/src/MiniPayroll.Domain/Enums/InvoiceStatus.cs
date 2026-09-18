namespace MiniPayroll.Domain.Enums;

public enum InvoiceStatus
{
    Draft = 0,
    Issued = 1,
    PaymentPending = 2,
    Paid = 3,
    PartiallyPaid = 4,
    Failed = 5,
    Void = 6,
    Refunded = 7
}
