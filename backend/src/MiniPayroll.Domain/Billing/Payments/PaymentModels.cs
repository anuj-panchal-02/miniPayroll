namespace MiniPayroll.Domain.Billing.Payments;

public sealed record PaymentCheckoutRequest(
    Guid CompanyId,
    decimal Amount,
    string Currency,
    string IdempotencyKey,
    Guid? InvoiceId = null,
    string? ReturnUrl = null);

public sealed record PaymentCheckoutResult(
    PaymentProviderStatus Status,
    string? CheckoutUrl = null,
    string? ProviderPaymentId = null,
    string? Error = null,
    string? ProviderOrderId = null,
    string? ClientKey = null);

public sealed record PaymentRecurringRequest(
    Guid CompanyId,
    string PlanCode,
    string Currency,
    decimal Amount,
    string IdempotencyKey,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd);

public sealed record PaymentRecurringResult(
    PaymentProviderStatus Status,
    string? ProviderSubscriptionId = null,
    string? Error = null);

public sealed record PaymentCancelRecurringRequest(
    string ProviderSubscriptionId,
    string IdempotencyKey);

public sealed record PaymentCancelRecurringResult(
    PaymentProviderStatus Status,
    string? ProviderSubscriptionId = null,
    string? Error = null);

public sealed record PaymentVerificationRequest(
    string ProviderPaymentId,
    string? Signature = null,
    string? Payload = null,
    string? ProviderOrderId = null);

public sealed record PaymentRecurringVerificationRequest(
    string ProviderSubscriptionId,
    string ProviderPaymentId,
    string? Signature = null);

public sealed record PaymentVerificationResult(
    PaymentProviderStatus Status,
    string? ProviderPaymentId = null,
    decimal? Amount = null,
    string? Error = null,
    string? Currency = null,
    string? ProviderOrderId = null,
    string? ProviderSubscriptionId = null,
    Guid? CompanyId = null);

public sealed record PaymentStatusRequest(string ProviderPaymentId);

public sealed record PaymentStatusResult(
    PaymentProviderStatus Status,
    PaymentLifecycleStatus PaymentStatus,
    string? ProviderPaymentId = null,
    string? Error = null);

public sealed record PaymentWebhookRequest(
    string RawBody,
    string? Signature,
    IReadOnlyDictionary<string, string> Headers);

public sealed record PaymentWebhookEvent(
    PaymentWebhookEventType Type,
    PaymentProviderStatus Status,
    string? ProviderPaymentId = null,
    decimal? Amount = null,
    string? Error = null,
    string? ProviderEventId = null,
    string? ProviderOrderId = null,
    string? ProviderSubscriptionId = null,
    string? Currency = null,
    Guid? CompanyId = null);
