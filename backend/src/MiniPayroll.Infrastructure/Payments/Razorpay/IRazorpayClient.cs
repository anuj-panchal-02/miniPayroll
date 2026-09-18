namespace MiniPayroll.Infrastructure.Payments.Razorpay;

public interface IRazorpayClient
{
    Task<RazorpayOrderRecord> CreateOrderAsync(
        RazorpayOrderCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<RazorpayOrderRecord> FetchOrderAsync(
        string orderId,
        CancellationToken cancellationToken = default);

    Task<RazorpayPaymentRecord> FetchPaymentAsync(
        string paymentId,
        CancellationToken cancellationToken = default);

    Task<RazorpaySubscriptionRecord> CreateSubscriptionAsync(
        RazorpaySubscriptionCreateRequest request,
        CancellationToken cancellationToken = default);

    Task<RazorpaySubscriptionRecord> FetchSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default);

    Task<RazorpaySubscriptionRecord> CancelSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default);

    bool VerifyPaymentSignature(string orderId, string paymentId, string signature);

    bool VerifySubscriptionSignature(string subscriptionId, string paymentId, string signature);

    bool VerifyWebhookSignature(string rawBody, string signature, string webhookSecret);
}
