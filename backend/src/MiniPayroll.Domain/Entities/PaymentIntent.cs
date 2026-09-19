using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Entities;

public class PaymentIntent
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid SubscriptionId { get; set; }
    public Guid? InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public string? ProviderOrderId { get; set; }
    public string? ProviderPaymentId { get; set; }
    public string? ProviderSubscriptionId { get; set; }
    public string? ProviderPaymentLinkId { get; set; }
    public string? CheckoutUrl { get; set; }
    public PaymentIntentStatus Status { get; set; } = PaymentIntentStatus.Created;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Company Company { get; set; } = null!;
    public Subscription Subscription { get; set; } = null!;
    public Invoice? Invoice { get; set; }
}
