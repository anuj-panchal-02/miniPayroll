using MiniPayroll.Domain.Billing.Payments;

namespace MiniPayroll.Infrastructure.Payments;

public sealed class PaymentGatewayService(IPaymentProvider provider)
{
    public Task<PaymentCheckoutResult> CreateCheckoutAsync(
        PaymentCheckoutRequest request,
        CancellationToken cancellationToken = default) =>
        provider.CreateCheckoutAsync(request, cancellationToken);

    public Task<PaymentLinkResult> CreatePaymentLinkAsync(
        PaymentLinkRequest request,
        CancellationToken cancellationToken = default) =>
        provider.CreatePaymentLinkAsync(request, cancellationToken);

    public Task<PaymentRecurringResult> CreateRecurringAsync(
        PaymentRecurringRequest request,
        CancellationToken cancellationToken = default) =>
        provider.CreateRecurringAsync(request, cancellationToken);

    public Task<PaymentCancelRecurringResult> CancelRecurringAsync(
        PaymentCancelRecurringRequest request,
        CancellationToken cancellationToken = default) =>
        provider.CancelRecurringAsync(request, cancellationToken);

    public Task<PaymentVerificationResult> VerifyPaymentAsync(
        PaymentVerificationRequest request,
        CancellationToken cancellationToken = default) =>
        provider.VerifyPaymentAsync(request, cancellationToken);

    public Task<PaymentVerificationResult> VerifyRecurringAsync(
        PaymentRecurringVerificationRequest request,
        CancellationToken cancellationToken = default) =>
        provider.VerifyRecurringAsync(request, cancellationToken);

    public Task<PaymentStatusResult> GetPaymentStatusAsync(
        PaymentStatusRequest request,
        CancellationToken cancellationToken = default) =>
        provider.GetPaymentStatusAsync(request, cancellationToken);

    public Task<PaymentWebhookEvent> HandleWebhookAsync(
        PaymentWebhookRequest request,
        CancellationToken cancellationToken = default) =>
        provider.HandleWebhookAsync(request, cancellationToken);
}
