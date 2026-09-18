using MiniPayroll.Domain.Billing.Payments;

namespace MiniPayroll.Infrastructure.Payments;

public sealed class FakePaymentProvider : IPaymentProvider
{
    private readonly Queue<PaymentProviderStatus> outcomes = new();
    private readonly Dictionary<string, PaymentCheckoutResult> checkouts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PaymentRecurringResult> recurring = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PaymentCancelRecurringResult> cancellations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PaymentVerificationResult> payments = new(StringComparer.Ordinal);
    private int sequence;

    public int CancelRecurringCalls { get; private set; }

    public FakePaymentProvider Next(PaymentProviderStatus status)
    {
        outcomes.Enqueue(status);
        return this;
    }

    public Task<PaymentCheckoutResult> CreateCheckoutAsync(
        PaymentCheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (checkouts.TryGetValue(request.IdempotencyKey, out var existing))
        {
            return Task.FromResult(existing with { Status = PaymentProviderStatus.Duplicate });
        }

        var outcome = Dequeue();
        var result = outcome switch
        {
            PaymentProviderStatus.Succeeded => SucceededCheckout(),
            PaymentProviderStatus.Failed => new PaymentCheckoutResult(
                PaymentProviderStatus.Failed,
                Error: "Checkout failed."),
            PaymentProviderStatus.Timeout => new PaymentCheckoutResult(
                PaymentProviderStatus.Timeout,
                Error: "Checkout timed out."),
            _ => new PaymentCheckoutResult(
                PaymentProviderStatus.Unavailable,
                Error: "Payment provider unavailable.")
        };

        if (result.Status == PaymentProviderStatus.Succeeded && result.ProviderPaymentId is { } paymentId)
        {
            payments[paymentId] = new PaymentVerificationResult(
                PaymentProviderStatus.Succeeded,
                paymentId,
                request.Amount);
        }

        checkouts[request.IdempotencyKey] = result;
        return Task.FromResult(result);
    }

    public Task<PaymentRecurringResult> CreateRecurringAsync(
        PaymentRecurringRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (recurring.TryGetValue(request.IdempotencyKey, out var existing))
        {
            return Task.FromResult(existing with { Status = PaymentProviderStatus.Duplicate });
        }

        var outcome = Dequeue();
        var result = outcome == PaymentProviderStatus.Succeeded
            ? new PaymentRecurringResult(PaymentProviderStatus.Succeeded, NextId("sub"))
            : new PaymentRecurringResult(outcome, Error: ErrorFor(outcome, "Recurring create"));
        recurring[request.IdempotencyKey] = result;
        return Task.FromResult(result);
    }

    public Task<PaymentCancelRecurringResult> CancelRecurringAsync(
        PaymentCancelRecurringRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CancelRecurringCalls++;
        if (cancellations.TryGetValue(request.IdempotencyKey, out var existing))
        {
            return Task.FromResult(existing with { Status = PaymentProviderStatus.Duplicate });
        }

        var outcome = Dequeue();
        var known = recurring.Values.Any(item => item.ProviderSubscriptionId == request.ProviderSubscriptionId);
        var result = outcome == PaymentProviderStatus.Succeeded && known
            ? new PaymentCancelRecurringResult(
                PaymentProviderStatus.Succeeded,
                request.ProviderSubscriptionId)
            : new PaymentCancelRecurringResult(
                outcome == PaymentProviderStatus.Succeeded ? PaymentProviderStatus.Failed : outcome,
                request.ProviderSubscriptionId,
                ErrorFor(outcome == PaymentProviderStatus.Succeeded ? PaymentProviderStatus.Failed : outcome, "Cancel"));
        cancellations[request.IdempotencyKey] = result;
        return Task.FromResult(result);
    }

    public Task<PaymentVerificationResult> VerifyPaymentAsync(
        PaymentVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Dequeue() is var outcome && outcome != PaymentProviderStatus.Succeeded)
        {
            return Task.FromResult(new PaymentVerificationResult(outcome, request.ProviderPaymentId, Error: ErrorFor(outcome, "Verify")));
        }

        if (payments.TryGetValue(request.ProviderPaymentId, out var verified))
        {
            return Task.FromResult(verified);
        }

        return Task.FromResult(new PaymentVerificationResult(
            PaymentProviderStatus.Failed,
            request.ProviderPaymentId,
            Error: "Payment was not found."));
    }

    public Task<PaymentVerificationResult> VerifyRecurringAsync(
        PaymentRecurringVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Dequeue() is var outcome && outcome != PaymentProviderStatus.Succeeded)
        {
            return Task.FromResult(new PaymentVerificationResult(
                outcome,
                request.ProviderPaymentId,
                Error: ErrorFor(outcome, "Verify"),
                ProviderSubscriptionId: request.ProviderSubscriptionId));
        }

        var known = recurring.Values.Any(item => item.ProviderSubscriptionId == request.ProviderSubscriptionId);
        return Task.FromResult(known
            ? new PaymentVerificationResult(
                PaymentProviderStatus.Succeeded,
                request.ProviderPaymentId,
                499m,
                Currency: "INR",
                ProviderSubscriptionId: request.ProviderSubscriptionId)
            : new PaymentVerificationResult(
                PaymentProviderStatus.Failed,
                request.ProviderPaymentId,
                Error: "Payment was not found.",
                ProviderSubscriptionId: request.ProviderSubscriptionId));
    }

    public Task<PaymentStatusResult> GetPaymentStatusAsync(
        PaymentStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var outcome = Dequeue();
        if (outcome != PaymentProviderStatus.Succeeded)
        {
            return Task.FromResult(new PaymentStatusResult(
                outcome,
                PaymentLifecycleStatus.Unknown,
                request.ProviderPaymentId,
                ErrorFor(outcome, "Status")));
        }

        if (payments.TryGetValue(request.ProviderPaymentId, out var verified))
        {
            return Task.FromResult(new PaymentStatusResult(
                PaymentProviderStatus.Succeeded,
                PaymentLifecycleStatus.Succeeded,
                verified.ProviderPaymentId));
        }

        return Task.FromResult(new PaymentStatusResult(
            PaymentProviderStatus.Succeeded,
            PaymentLifecycleStatus.Unknown,
            request.ProviderPaymentId));
    }

    public Task<PaymentWebhookEvent> HandleWebhookAsync(
        PaymentWebhookRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var outcome = Dequeue();
        if (outcome != PaymentProviderStatus.Succeeded)
        {
            return Task.FromResult(new PaymentWebhookEvent(
                PaymentWebhookEventType.Ignored,
                outcome,
                Error: ErrorFor(outcome, "Webhook")));
        }

        var paymentId = payments.Keys.LastOrDefault();
        return Task.FromResult(new PaymentWebhookEvent(
            PaymentWebhookEventType.PaymentSucceeded,
            PaymentProviderStatus.Succeeded,
            paymentId,
            paymentId is null ? null : payments[paymentId].Amount));
    }

    private PaymentProviderStatus Dequeue() =>
        outcomes.Count > 0 ? outcomes.Dequeue() : PaymentProviderStatus.Succeeded;

    private PaymentCheckoutResult SucceededCheckout()
    {
        var paymentId = NextId("pay");
        return new PaymentCheckoutResult(
            PaymentProviderStatus.Succeeded,
            CheckoutUrl: $"https://payments.test/checkout/{paymentId}",
            ProviderPaymentId: paymentId,
            ProviderOrderId: NextId("order"));
    }

    private string NextId(string prefix) => $"{prefix}_{++sequence:D6}";

    private static string ErrorFor(PaymentProviderStatus status, string action) =>
        status switch
        {
            PaymentProviderStatus.Failed => $"{action} failed.",
            PaymentProviderStatus.Timeout => $"{action} timed out.",
            PaymentProviderStatus.Unavailable => "Payment provider unavailable.",
            _ => $"{action} could not be completed."
        };
}
