using MiniPayroll.Domain.Billing.Payments;
using MiniPayroll.Infrastructure.Payments;

namespace MiniPayroll.Tests;

public sealed class PaymentProviderTests
{
    [Fact]
    public async Task Successful_checkout_returns_a_url_and_provider_payment_id()
    {
        var gateway = new PaymentGatewayService(new FakePaymentProvider());
        var result = await gateway.CreateCheckoutAsync(Checkout());

        Assert.Equal(PaymentProviderStatus.Succeeded, result.Status);
        Assert.StartsWith("https://payments.test/checkout/", result.CheckoutUrl);
        Assert.False(string.IsNullOrWhiteSpace(result.ProviderPaymentId));
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task Failed_checkout_has_no_url()
    {
        var gateway = new PaymentGatewayService(new FakePaymentProvider().Next(PaymentProviderStatus.Failed));
        var result = await gateway.CreateCheckoutAsync(Checkout());

        Assert.Equal(PaymentProviderStatus.Failed, result.Status);
        Assert.Null(result.CheckoutUrl);
        Assert.Null(result.ProviderPaymentId);
        Assert.Equal("Checkout failed.", result.Error);
    }

    [Fact]
    public async Task Verification_succeeds_for_a_completed_checkout_and_fails_for_an_unknown_id()
    {
        var fake = new FakePaymentProvider();
        var gateway = new PaymentGatewayService(fake);
        var checkout = await gateway.CreateCheckoutAsync(Checkout());

        var verified = await gateway.VerifyPaymentAsync(new PaymentVerificationRequest(checkout.ProviderPaymentId!));
        Assert.Equal(PaymentProviderStatus.Succeeded, verified.Status);
        Assert.Equal(checkout.ProviderPaymentId, verified.ProviderPaymentId);
        Assert.Equal(499m, verified.Amount);

        var missing = await gateway.VerifyPaymentAsync(new PaymentVerificationRequest("pay_missing"));
        Assert.Equal(PaymentProviderStatus.Failed, missing.Status);
        Assert.Equal("Payment was not found.", missing.Error);
    }

    [Fact]
    public async Task Recurring_create_then_cancel_succeeds()
    {
        var gateway = new PaymentGatewayService(new FakePaymentProvider());
        var created = await gateway.CreateRecurringAsync(new PaymentRecurringRequest(
            Guid.NewGuid(),
            "starter",
            "INR",
            499m,
            "recur-1",
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 1, 31, 23, 59, 59, TimeSpan.Zero)));

        Assert.Equal(PaymentProviderStatus.Succeeded, created.Status);
        Assert.StartsWith("sub_", created.ProviderSubscriptionId);

        var cancelled = await gateway.CancelRecurringAsync(
            new PaymentCancelRecurringRequest(created.ProviderSubscriptionId!, "cancel-1"));
        Assert.Equal(PaymentProviderStatus.Succeeded, cancelled.Status);
        Assert.Equal(created.ProviderSubscriptionId, cancelled.ProviderSubscriptionId);
    }

    [Fact]
    public async Task Provider_failure_is_unavailable()
    {
        var gateway = new PaymentGatewayService(new FakePaymentProvider().Next(PaymentProviderStatus.Unavailable));
        var result = await gateway.CreateCheckoutAsync(Checkout());

        Assert.Equal(PaymentProviderStatus.Unavailable, result.Status);
        Assert.Equal("Payment provider unavailable.", result.Error);
        Assert.Null(result.CheckoutUrl);
    }

    [Fact]
    public async Task Timeout_is_an_immediate_result_status()
    {
        var gateway = new PaymentGatewayService(new FakePaymentProvider().Next(PaymentProviderStatus.Timeout));
        var started = DateTime.UtcNow;
        var result = await gateway.CreateCheckoutAsync(Checkout());
        var elapsed = DateTime.UtcNow - started;

        Assert.Equal(PaymentProviderStatus.Timeout, result.Status);
        Assert.Equal("Checkout timed out.", result.Error);
        Assert.True(elapsed < TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task Duplicate_idempotency_key_replays_the_same_provider_payment()
    {
        var gateway = new PaymentGatewayService(new FakePaymentProvider());
        var first = await gateway.CreateCheckoutAsync(Checkout("dup-1"));
        var replay = await gateway.CreateCheckoutAsync(Checkout("dup-1"));

        Assert.Equal(PaymentProviderStatus.Succeeded, first.Status);
        Assert.Equal(PaymentProviderStatus.Duplicate, replay.Status);
        Assert.Equal(first.ProviderPaymentId, replay.ProviderPaymentId);
        Assert.Equal(first.CheckoutUrl, replay.CheckoutUrl);
    }

    [Fact]
    public async Task Razorpay_stub_stays_offline_and_never_throws()
    {
        var razorpay = new RazorpayPaymentProvider();
        var gateway = new PaymentGatewayService(razorpay);

        var checkout = await gateway.CreateCheckoutAsync(Checkout());
        var recurring = await gateway.CreateRecurringAsync(new PaymentRecurringRequest(
            Guid.NewGuid(),
            "starter",
            "INR",
            499m,
            "rzp-1",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMonths(1)));
        var cancelled = await gateway.CancelRecurringAsync(new PaymentCancelRecurringRequest("sub_x", "rzp-c"));
        var verified = await gateway.VerifyPaymentAsync(new PaymentVerificationRequest("pay_x"));
        var recurringVerified = await gateway.VerifyRecurringAsync(
            new PaymentRecurringVerificationRequest("sub_x", "pay_x"));
        var status = await gateway.GetPaymentStatusAsync(new PaymentStatusRequest("pay_x"));
        var webhook = await gateway.HandleWebhookAsync(
            new PaymentWebhookRequest("{}", null, new Dictionary<string, string>()));

        Assert.Equal(PaymentProviderStatus.Unavailable, checkout.Status);
        Assert.Equal(RazorpayPaymentProvider.NotConfigured, checkout.Error);
        Assert.Null(checkout.CheckoutUrl);
        Assert.Equal(PaymentProviderStatus.Unavailable, recurring.Status);
        Assert.Equal(PaymentProviderStatus.Unavailable, cancelled.Status);
        Assert.Equal(PaymentProviderStatus.Unavailable, verified.Status);
        Assert.Equal(PaymentProviderStatus.Unavailable, recurringVerified.Status);
        Assert.Equal(PaymentProviderStatus.Unavailable, status.Status);
        Assert.Equal(PaymentLifecycleStatus.Unknown, status.PaymentStatus);
        Assert.Equal(PaymentWebhookEventType.Ignored, webhook.Type);
        Assert.Equal(typeof(RazorpayPaymentProvider), razorpay.GetType());
        Assert.IsAssignableFrom<IPaymentProvider>(razorpay);
    }

    [Fact]
    public void Domain_project_does_not_reference_provider_sdks()
    {
        var csproj = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "MiniPayroll.Domain", "MiniPayroll.Domain.csproj"));
        var text = File.ReadAllText(csproj);
        Assert.DoesNotContain("Razorpay", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Stripe", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PackageReference", text, StringComparison.Ordinal);

        var infrastructure = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "MiniPayroll.Infrastructure", "MiniPayroll.Infrastructure.csproj"));
        Assert.Contains("Razorpay", File.ReadAllText(infrastructure), StringComparison.Ordinal);
        Assert.Contains("3.3.2", File.ReadAllText(infrastructure), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Gateway_exposes_status_and_webhook_through_the_port_only()
    {
        var fake = new FakePaymentProvider();
        var gateway = new PaymentGatewayService(fake);
        var checkout = await gateway.CreateCheckoutAsync(Checkout());
        var status = await gateway.GetPaymentStatusAsync(new PaymentStatusRequest(checkout.ProviderPaymentId!));
        var webhook = await gateway.HandleWebhookAsync(
            new PaymentWebhookRequest("payment.succeeded", "sig", new Dictionary<string, string>()));

        Assert.Equal(PaymentLifecycleStatus.Succeeded, status.PaymentStatus);
        Assert.Equal(PaymentWebhookEventType.PaymentSucceeded, webhook.Type);
        Assert.Equal(checkout.ProviderPaymentId, webhook.ProviderPaymentId);
    }

    private static PaymentCheckoutRequest Checkout(string key = "checkout-1") =>
        new(Guid.NewGuid(), 499m, "INR", key, InvoiceId: Guid.NewGuid());
}
