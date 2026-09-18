using MiniPayroll.Infrastructure.Payments.Razorpay;

namespace MiniPayroll.Tests;

internal sealed class ScriptedRazorpayClient : IRazorpayClient
{
    public bool PaymentSignatureValid { get; set; } = true;
    public bool SubscriptionSignatureValid { get; set; } = true;
    public bool WebhookSignatureValid { get; set; } = true;
    public Exception? Throw { get; set; }
    public RazorpayOrderRecord? Order { get; set; }
    public RazorpayPaymentRecord? Payment { get; set; }
    public RazorpaySubscriptionRecord? Subscription { get; set; }
    public int OrdersCreated { get; private set; }
    public int SubscriptionsCancelled { get; private set; }

    public Task<RazorpayOrderRecord> CreateOrderAsync(
        RazorpayOrderCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ThrowIfNeeded();
        OrdersCreated++;
        Order ??= new RazorpayOrderRecord("order_1", request.AmountPaise, request.Currency, request.Notes);
        return Task.FromResult(Order);
    }

    public Task<RazorpayOrderRecord> FetchOrderAsync(string orderId, CancellationToken cancellationToken = default)
    {
        ThrowIfNeeded();
        return Task.FromResult(Order ?? new RazorpayOrderRecord(orderId, 49900, "INR", new Dictionary<string, string>()));
    }

    public Task<RazorpayPaymentRecord> FetchPaymentAsync(string paymentId, CancellationToken cancellationToken = default)
    {
        ThrowIfNeeded();
        return Task.FromResult(Payment ?? new RazorpayPaymentRecord(
            paymentId,
            Order?.Id ?? "order_1",
            "captured",
            49900,
            "INR",
            Order?.Notes ?? new Dictionary<string, string>()));
    }

    public Task<RazorpaySubscriptionRecord> CreateSubscriptionAsync(
        RazorpaySubscriptionCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        ThrowIfNeeded();
        Subscription ??= new RazorpaySubscriptionRecord("sub_1", "created", request.Notes);
        return Task.FromResult(Subscription);
    }

    public Task<RazorpaySubscriptionRecord> FetchSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfNeeded();
        return Task.FromResult(Subscription ?? new RazorpaySubscriptionRecord(subscriptionId, "active", new Dictionary<string, string>()));
    }

    public Task<RazorpaySubscriptionRecord> CancelSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        ThrowIfNeeded();
        SubscriptionsCancelled++;
        var cancelled = new RazorpaySubscriptionRecord(subscriptionId, "cancelled", new Dictionary<string, string>());
        Subscription = cancelled;
        return Task.FromResult(cancelled);
    }

    public bool VerifyPaymentSignature(string orderId, string paymentId, string signature) => PaymentSignatureValid;

    public bool VerifySubscriptionSignature(string subscriptionId, string paymentId, string signature) =>
        SubscriptionSignatureValid;

    public bool VerifyWebhookSignature(string rawBody, string signature, string webhookSecret) =>
        WebhookSignatureValid;

    private void ThrowIfNeeded()
    {
        if (Throw is not null)
        {
            throw Throw;
        }
    }
}
