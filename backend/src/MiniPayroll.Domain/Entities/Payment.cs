namespace MiniPayroll.Domain.Entities;

public class Payment
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid SubscriptionId { get; set; }
    public decimal Amount { get; set; }
    public DateTimeOffset PaidOn { get; set; }
    public string PaymentMode { get; set; } = string.Empty;
    public string? InvoiceGstReference { get; set; }
    public string BillingPeriod { get; set; } = string.Empty;
    public Guid RecordedByUserId { get; set; }
    public DateTimeOffset RecordedAt { get; set; }

    public Company Company { get; set; } = null!;
    public Subscription Subscription { get; set; } = null!;
}
