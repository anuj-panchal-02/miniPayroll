using MiniPayroll.Domain.Billing.Payments;
using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Entities;

public class PaymentProviderEvent
{
    public Guid Id { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? IntentId { get; set; }
    public Guid? SubscriptionId { get; set; }
    public Guid? InvoiceId { get; set; }
    public string Provider { get; set; } = PaymentProviders.Razorpay;
    public string ExternalEventId { get; set; } = string.Empty;
    public PaymentWebhookEventType EventType { get; set; } = PaymentWebhookEventType.Ignored;
    public PaymentWebhookProcessingStatus ProcessingStatus { get; set; } = PaymentWebhookProcessingStatus.Received;
    public string PayloadHash { get; set; } = string.Empty;
    public string? Error { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }

    public PaymentIntent? Intent { get; set; }
}
