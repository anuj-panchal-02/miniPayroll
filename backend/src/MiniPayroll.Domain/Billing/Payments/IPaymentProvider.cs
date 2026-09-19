namespace MiniPayroll.Domain.Billing.Payments;

public interface IPaymentProvider
{
    Task<PaymentCheckoutResult> CreateCheckoutAsync(
        PaymentCheckoutRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentLinkResult> CreatePaymentLinkAsync(
        PaymentLinkRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentRecurringResult> CreateRecurringAsync(
        PaymentRecurringRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentCancelRecurringResult> CancelRecurringAsync(
        PaymentCancelRecurringRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentVerificationResult> VerifyPaymentAsync(
        PaymentVerificationRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentVerificationResult> VerifyRecurringAsync(
        PaymentRecurringVerificationRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentStatusResult> GetPaymentStatusAsync(
        PaymentStatusRequest request,
        CancellationToken cancellationToken = default);

    Task<PaymentWebhookEvent> HandleWebhookAsync(
        PaymentWebhookRequest request,
        CancellationToken cancellationToken = default);
}
