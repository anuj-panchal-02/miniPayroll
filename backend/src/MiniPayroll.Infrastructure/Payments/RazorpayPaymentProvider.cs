using System.Collections.Concurrent;
using System.Text.Json;
using MiniPayroll.Domain.Billing.Payments;
using MiniPayroll.Infrastructure.Payments.Razorpay;

namespace MiniPayroll.Infrastructure.Payments;

public sealed class RazorpayPaymentProvider : IPaymentProvider
{
    public const string NotConfigured = "Razorpay is not configured.";
    public const string SignatureHeader = "X-Razorpay-Signature";

    private static readonly HashSet<string> CapturedEvents = new(StringComparer.OrdinalIgnoreCase)
    {
        "payment.captured",
        "payment_link.paid",
        "subscription.charged"
    };

    private readonly RazorpayOptions options;
    private readonly IRazorpayClient? client;
    private readonly ConcurrentDictionary<string, PaymentCheckoutResult> checkouts = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, PaymentLinkResult> paymentLinks = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, PaymentRecurringResult> recurring = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, PaymentCancelRecurringResult> cancellations = new(StringComparer.Ordinal);

    public RazorpayPaymentProvider()
        : this(new RazorpayOptions(), null)
    {
    }

    public RazorpayPaymentProvider(RazorpayOptions options, IRazorpayClient? client = null)
    {
        this.options = options;
        this.client = client;
    }

    public RazorpayPaymentProvider(
        Microsoft.Extensions.Options.IOptions<RazorpayOptions> options,
        IRazorpayClient client)
        : this(options.Value, client)
    {
    }

    public async Task<PaymentCheckoutResult> CreateCheckoutAsync(
        PaymentCheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Ready(out var unavailable))
        {
            return new PaymentCheckoutResult(PaymentProviderStatus.Unavailable, Error: unavailable);
        }

        if (checkouts.TryGetValue(request.IdempotencyKey, out var existing))
        {
            return existing with { Status = PaymentProviderStatus.Duplicate };
        }

        try
        {
            var order = await client!.CreateOrderAsync(
                new RazorpayOrderCreateRequest(
                    RazorpayMoney.ToPaise(request.Amount),
                    request.Currency,
                    request.IdempotencyKey,
                    Notes(request.CompanyId, request.InvoiceId)),
                cancellationToken);
            var result = new PaymentCheckoutResult(
                PaymentProviderStatus.Succeeded,
                ProviderOrderId: order.Id,
                ClientKey: options.KeyId);
            checkouts[request.IdempotencyKey] = result;
            return result;
        }
        catch (Exception exception)
        {
            return new PaymentCheckoutResult(StatusOf(exception), Error: exception.Message);
        }
    }

    public async Task<PaymentLinkResult> CreatePaymentLinkAsync(
        PaymentLinkRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Ready(out var unavailable))
        {
            return new PaymentLinkResult(PaymentProviderStatus.Unavailable, Error: unavailable);
        }

        if (paymentLinks.TryGetValue(request.IdempotencyKey, out var existing))
        {
            return existing with { Status = PaymentProviderStatus.Duplicate };
        }

        try
        {
            var notes = Notes(request.CompanyId, request.InvoiceId);
            notes["billingPeriod"] = request.BillingPeriod;
            var created = await client!.CreatePaymentLinkAsync(
                new RazorpayPaymentLinkCreateRequest(
                    RazorpayMoney.ToPaise(request.Amount),
                    request.Currency,
                    request.Description ?? $"miniPayroll {request.BillingPeriod}",
                    request.IdempotencyKey.Length <= 40
                        ? request.IdempotencyKey
                        : request.IdempotencyKey[^40..],
                    notes),
                cancellationToken);
            var result = new PaymentLinkResult(
                PaymentProviderStatus.Succeeded,
                created.ShortUrl,
                created.Id);
            paymentLinks[request.IdempotencyKey] = result;
            return result;
        }
        catch (Exception exception)
        {
            return new PaymentLinkResult(StatusOf(exception), Error: exception.Message);
        }
    }

    public async Task<PaymentRecurringResult> CreateRecurringAsync(
        PaymentRecurringRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Ready(out var unavailable))
        {
            return new PaymentRecurringResult(PaymentProviderStatus.Unavailable, Error: unavailable);
        }

        if (recurring.TryGetValue(request.IdempotencyKey, out var existing))
        {
            return existing with { Status = PaymentProviderStatus.Duplicate };
        }

        try
        {
            var created = await client!.CreateSubscriptionAsync(
                new RazorpaySubscriptionCreateRequest(
                    request.PlanCode,
                    120,
                    Notes(request.CompanyId, null, request.PlanCode)),
                cancellationToken);
            var result = new PaymentRecurringResult(PaymentProviderStatus.Succeeded, created.Id);
            recurring[request.IdempotencyKey] = result;
            return result;
        }
        catch (Exception exception)
        {
            return new PaymentRecurringResult(StatusOf(exception), Error: exception.Message);
        }
    }

    public async Task<PaymentCancelRecurringResult> CancelRecurringAsync(
        PaymentCancelRecurringRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Ready(out var unavailable))
        {
            return new PaymentCancelRecurringResult(PaymentProviderStatus.Unavailable, Error: unavailable);
        }

        if (cancellations.TryGetValue(request.IdempotencyKey, out var existing))
        {
            return existing with { Status = PaymentProviderStatus.Duplicate };
        }

        try
        {
            var cancelled = await client!.CancelSubscriptionAsync(request.ProviderSubscriptionId, cancellationToken);
            var result = new PaymentCancelRecurringResult(PaymentProviderStatus.Succeeded, cancelled.Id);
            cancellations[request.IdempotencyKey] = result;
            return result;
        }
        catch (Exception exception)
        {
            return new PaymentCancelRecurringResult(StatusOf(exception), Error: exception.Message);
        }
    }

    public async Task<PaymentVerificationResult> VerifyPaymentAsync(
        PaymentVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Ready(out var unavailable))
        {
            return new PaymentVerificationResult(PaymentProviderStatus.Unavailable, Error: unavailable);
        }

        if (string.IsNullOrWhiteSpace(request.ProviderOrderId)
            || string.IsNullOrWhiteSpace(request.Signature)
            || !client!.VerifyPaymentSignature(
                request.ProviderOrderId,
                request.ProviderPaymentId,
                request.Signature))
        {
            return new PaymentVerificationResult(
                PaymentProviderStatus.Failed,
                request.ProviderPaymentId,
                Error: "Payment signature was not valid.");
        }

        return await FetchVerifiedPaymentAsync(
            request.ProviderPaymentId,
            request.ProviderOrderId,
            cancellationToken);
    }

    public async Task<PaymentVerificationResult> VerifyRecurringAsync(
        PaymentRecurringVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Ready(out var unavailable))
        {
            return new PaymentVerificationResult(PaymentProviderStatus.Unavailable, Error: unavailable);
        }

        if (string.IsNullOrWhiteSpace(request.Signature)
            || !client!.VerifySubscriptionSignature(
                request.ProviderSubscriptionId,
                request.ProviderPaymentId,
                request.Signature))
        {
            return new PaymentVerificationResult(
                PaymentProviderStatus.Failed,
                request.ProviderPaymentId,
                Error: "Subscription signature was not valid.",
                ProviderSubscriptionId: request.ProviderSubscriptionId);
        }

        try
        {
            var subscription = await client.FetchSubscriptionAsync(request.ProviderSubscriptionId, cancellationToken);
            var verified = await FetchVerifiedPaymentAsync(request.ProviderPaymentId, null, cancellationToken);
            return verified with { ProviderSubscriptionId = subscription.Id };
        }
        catch (Exception exception)
        {
            return new PaymentVerificationResult(
                StatusOf(exception),
                request.ProviderPaymentId,
                Error: exception.Message,
                ProviderSubscriptionId: request.ProviderSubscriptionId);
        }
    }

    public async Task<PaymentStatusResult> GetPaymentStatusAsync(
        PaymentStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Ready(out var unavailable))
        {
            return new PaymentStatusResult(
                PaymentProviderStatus.Unavailable,
                PaymentLifecycleStatus.Unknown,
                Error: unavailable);
        }

        try
        {
            var payment = await client!.FetchPaymentAsync(request.ProviderPaymentId, cancellationToken);
            return new PaymentStatusResult(
                PaymentProviderStatus.Succeeded,
                MapLifecycle(payment.Status),
                payment.Id);
        }
        catch (Exception exception)
        {
            return new PaymentStatusResult(
                StatusOf(exception),
                PaymentLifecycleStatus.Unknown,
                request.ProviderPaymentId,
                exception.Message);
        }
    }

    public async Task<PaymentWebhookEvent> HandleWebhookAsync(
        PaymentWebhookRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Ready(out var unavailable) || !options.HasWebhookSecret)
        {
            return new PaymentWebhookEvent(
                PaymentWebhookEventType.Ignored,
                PaymentProviderStatus.Unavailable,
                Error: unavailable ?? NotConfigured);
        }

        var signature = Signature(request);
        if (string.IsNullOrWhiteSpace(signature)
            || !client!.VerifyWebhookSignature(request.RawBody, signature, options.WebhookSecret))
        {
            return new PaymentWebhookEvent(
                PaymentWebhookEventType.Ignored,
                PaymentProviderStatus.Failed,
                Error: "Webhook signature was not valid.");
        }

        if (!TryReadWebhook(request.RawBody, out var parsed))
        {
            return new PaymentWebhookEvent(
                PaymentWebhookEventType.Ignored,
                PaymentProviderStatus.Succeeded,
                Error: "Webhook payload was ignored.");
        }

        try
        {
            return parsed.Event switch
            {
                "payment.failed" => LifecycleEvent(PaymentWebhookEventType.PaymentFailed, parsed),
                "subscription.cancelled" => LifecycleEvent(PaymentWebhookEventType.RecurringCancelled, parsed),
                "subscription.activated" => LifecycleEvent(PaymentWebhookEventType.SubscriptionActivated, parsed),
                "subscription.paused" or "subscription.halted" =>
                    LifecycleEvent(PaymentWebhookEventType.SubscriptionPaused, parsed),
                "subscription.resumed" => LifecycleEvent(PaymentWebhookEventType.SubscriptionResumed, parsed),
                "refund.processed" or "payment.refunded" => LifecycleEvent(PaymentWebhookEventType.Refunded, parsed),
                _ when CapturedEvents.Contains(parsed.Event) =>
                    await CapturedWebhookAsync(parsed, cancellationToken),
                _ => new PaymentWebhookEvent(
                    PaymentWebhookEventType.Ignored,
                    PaymentProviderStatus.Succeeded,
                    parsed.PaymentId,
                    ProviderEventId: parsed.EventId,
                    ProviderOrderId: parsed.OrderId,
                    ProviderSubscriptionId: parsed.SubscriptionId)
            };
        }
        catch (Exception exception)
        {
            return new PaymentWebhookEvent(
                PaymentWebhookEventType.Ignored,
                StatusOf(exception),
                parsed.PaymentId,
                Error: exception.Message,
                ProviderEventId: parsed.EventId);
        }
    }

    private static PaymentWebhookEvent LifecycleEvent(PaymentWebhookEventType type, WebhookPayload parsed) =>
        new(
            type,
            PaymentProviderStatus.Succeeded,
            parsed.PaymentId,
            parsed.Amount,
            ProviderEventId: parsed.EventId,
            ProviderOrderId: parsed.OrderId,
            ProviderSubscriptionId: parsed.SubscriptionId,
            Currency: parsed.Currency,
            CompanyId: parsed.CompanyId,
            ProviderPaymentLinkId: parsed.PaymentLinkId);

    private async Task<PaymentWebhookEvent> CapturedWebhookAsync(
        WebhookPayload parsed,
        CancellationToken cancellationToken)
    {
        if (parsed.Event.Equals("payment.authorized", StringComparison.OrdinalIgnoreCase))
        {
            return new PaymentWebhookEvent(
                PaymentWebhookEventType.Ignored,
                PaymentProviderStatus.Succeeded,
                parsed.PaymentId,
                ProviderEventId: parsed.EventId,
                ProviderPaymentLinkId: parsed.PaymentLinkId);
        }

        if (string.IsNullOrWhiteSpace(parsed.PaymentId)
            && !string.IsNullOrWhiteSpace(parsed.PaymentLinkId)
            && parsed.Amount is { } linkAmount)
        {
            return new PaymentWebhookEvent(
                PaymentWebhookEventType.PaymentSucceeded,
                PaymentProviderStatus.Succeeded,
                null,
                linkAmount,
                ProviderEventId: parsed.EventId,
                ProviderOrderId: parsed.OrderId,
                Currency: parsed.Currency,
                CompanyId: parsed.CompanyId,
                ProviderPaymentLinkId: parsed.PaymentLinkId);
        }

        if (string.IsNullOrWhiteSpace(parsed.PaymentId))
        {
            return new PaymentWebhookEvent(
                PaymentWebhookEventType.Ignored,
                PaymentProviderStatus.Succeeded,
                ProviderEventId: parsed.EventId,
                ProviderPaymentLinkId: parsed.PaymentLinkId);
        }

        var payment = await client!.FetchPaymentAsync(parsed.PaymentId, cancellationToken);
        if (!IsCaptured(payment.Status))
        {
            return new PaymentWebhookEvent(
                PaymentWebhookEventType.Ignored,
                PaymentProviderStatus.Succeeded,
                payment.Id,
                RazorpayMoney.FromPaise(payment.AmountPaise),
                ProviderEventId: parsed.EventId,
                ProviderOrderId: payment.OrderId,
                ProviderSubscriptionId: payment.SubscriptionId ?? parsed.SubscriptionId,
                Currency: payment.Currency,
                CompanyId: CompanyId(payment.Notes),
                ProviderPaymentLinkId: parsed.PaymentLinkId);
        }

        return new PaymentWebhookEvent(
            PaymentWebhookEventType.PaymentSucceeded,
            PaymentProviderStatus.Succeeded,
            payment.Id,
            RazorpayMoney.FromPaise(payment.AmountPaise),
            ProviderEventId: parsed.EventId,
            ProviderOrderId: payment.OrderId ?? parsed.OrderId,
            ProviderSubscriptionId: payment.SubscriptionId ?? parsed.SubscriptionId,
            Currency: payment.Currency,
            CompanyId: CompanyId(payment.Notes) ?? parsed.CompanyId,
            ProviderPaymentLinkId: parsed.PaymentLinkId);
    }

    private async Task<PaymentVerificationResult> FetchVerifiedPaymentAsync(
        string paymentId,
        string? expectedOrderId,
        CancellationToken cancellationToken)
    {
        try
        {
            var payment = await client!.FetchPaymentAsync(paymentId, cancellationToken);
            if (!IsCaptured(payment.Status))
            {
                return new PaymentVerificationResult(
                    PaymentProviderStatus.Failed,
                    payment.Id,
                    RazorpayMoney.FromPaise(payment.AmountPaise),
                    "Payment has not been captured.",
                    payment.Currency,
                    payment.OrderId,
                    payment.SubscriptionId,
                    CompanyId(payment.Notes));
            }

            if (expectedOrderId is not null
                && !string.Equals(payment.OrderId, expectedOrderId, StringComparison.Ordinal))
            {
                return new PaymentVerificationResult(
                    PaymentProviderStatus.Failed,
                    payment.Id,
                    Error: "Payment order did not match.");
            }

            return new PaymentVerificationResult(
                PaymentProviderStatus.Succeeded,
                payment.Id,
                RazorpayMoney.FromPaise(payment.AmountPaise),
                Currency: payment.Currency,
                ProviderOrderId: payment.OrderId,
                ProviderSubscriptionId: payment.SubscriptionId,
                CompanyId: CompanyId(payment.Notes));
        }
        catch (Exception exception)
        {
            return new PaymentVerificationResult(StatusOf(exception), paymentId, Error: exception.Message);
        }
    }

    private bool Ready(out string? error)
    {
        if (!options.IsConfigured || client is null)
        {
            error = NotConfigured;
            return false;
        }

        error = null;
        return true;
    }

    private static string? Signature(PaymentWebhookRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Signature))
        {
            return request.Signature;
        }

        return request.Headers.TryGetValue(SignatureHeader, out var header)
            || request.Headers.TryGetValue("x-razorpay-signature", out header)
            ? header
            : null;
    }

    private static Dictionary<string, string> Notes(Guid companyId, Guid? invoiceId, string? planCode = null)
    {
        var notes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["companyId"] = companyId.ToString("D")
        };
        if (invoiceId is { } invoice)
        {
            notes["invoiceId"] = invoice.ToString("D");
        }

        if (!string.IsNullOrWhiteSpace(planCode))
        {
            notes["planCode"] = planCode;
        }

        return notes;
    }

    private static Guid? CompanyId(IReadOnlyDictionary<string, string> notes) =>
        notes.TryGetValue("companyId", out var value) && Guid.TryParse(value, out var companyId)
            ? companyId
            : null;

    private static bool IsCaptured(string status) =>
        string.Equals(status, "captured", StringComparison.OrdinalIgnoreCase);

    private static PaymentLifecycleStatus MapLifecycle(string status) =>
        status.ToLowerInvariant() switch
        {
            "captured" => PaymentLifecycleStatus.Succeeded,
            "failed" => PaymentLifecycleStatus.Failed,
            "authorized" or "created" or "pending" => PaymentLifecycleStatus.Pending,
            _ => PaymentLifecycleStatus.Unknown
        };

    private static PaymentProviderStatus StatusOf(Exception exception) =>
        exception is RazorpayClientException client && client.Status == "Timeout"
            ? PaymentProviderStatus.Timeout
            : exception is TimeoutException or OperationCanceledException
                ? PaymentProviderStatus.Timeout
                : PaymentProviderStatus.Unavailable;

    private static bool TryReadWebhook(string rawBody, out WebhookPayload payload)
    {
        payload = default;
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(rawBody) ? "{}" : rawBody);
            var root = document.RootElement;
            var eventName = root.TryGetProperty("event", out var name) ? name.GetString() ?? string.Empty : string.Empty;
            var eventId = root.TryGetProperty("id", out var id) ? id.GetString() : null;
            var payment = Entity(root, "payment");
            var subscription = Entity(root, "subscription");
            var refund = Entity(root, "refund");
            var paymentLink = Entity(root, "payment_link");
            var paymentId = StringAt(payment, "id") ?? StringAt(refund, "payment_id");
            var orderId = StringAt(payment, "order_id") ?? StringAt(paymentLink, "order_id");
            var paymentLinkId = StringAt(paymentLink, "id");
            var subscriptionId = StringAt(subscription, "id") ?? StringAt(payment, "subscription_id");
            var amount = IntAt(payment, "amount") ?? IntAt(paymentLink, "amount") ?? IntAt(refund, "amount");
            var currency = StringAt(payment, "currency")
                ?? StringAt(paymentLink, "currency")
                ?? StringAt(refund, "currency");
            var companyId = GuidAt(NotesAt(payment), "companyId")
                ?? GuidAt(NotesAt(paymentLink), "companyId")
                ?? GuidAt(NotesAt(subscription), "companyId")
                ?? GuidAt(NotesAt(refund), "companyId");
            payload = new WebhookPayload(
                eventName,
                eventId ?? $"{eventName}:{paymentId ?? paymentLinkId ?? subscriptionId}",
                paymentId,
                orderId,
                subscriptionId,
                amount is null ? null : RazorpayMoney.FromPaise(amount.Value),
                currency,
                companyId,
                paymentLinkId);
            return !string.IsNullOrWhiteSpace(eventName);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static JsonElement? Entity(JsonElement root, string name)
    {
        if (!root.TryGetProperty("payload", out var payload)
            || !payload.TryGetProperty(name, out var wrapper)
            || !wrapper.TryGetProperty("entity", out var entity))
        {
            return null;
        }

        return entity;
    }

    private static string? StringAt(JsonElement? entity, string name) =>
        entity is { } value && value.TryGetProperty(name, out var property)
            ? property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString()
            : null;

    private static int? IntAt(JsonElement? entity, string name)
    {
        if (entity is not { } value || !value.TryGetProperty(name, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number when property.TryGetInt32(out var paise) => paise,
            JsonValueKind.String when int.TryParse(property.GetString(), out var parsed) => parsed,
            _ => null
        };
    }

    private static JsonElement? NotesAt(JsonElement? entity) =>
        entity is { } value && value.TryGetProperty("notes", out var notes) ? notes : null;

    private static Guid? GuidAt(JsonElement? notes, string name) =>
        notes is { } value
        && value.TryGetProperty(name, out var property)
        && Guid.TryParse(property.GetString(), out var parsed)
            ? parsed
            : null;

    private readonly record struct WebhookPayload(
        string Event,
        string EventId,
        string? PaymentId,
        string? OrderId,
        string? SubscriptionId,
        decimal? Amount,
        string? Currency,
        Guid? CompanyId,
        string? PaymentLinkId);
}
