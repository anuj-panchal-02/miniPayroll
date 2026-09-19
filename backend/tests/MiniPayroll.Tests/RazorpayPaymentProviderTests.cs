using MiniPayroll.Domain.Billing.Payments;
using MiniPayroll.Infrastructure.Payments;
using MiniPayroll.Infrastructure.Payments.Razorpay;

namespace MiniPayroll.Tests;

public sealed class RazorpayPaymentProviderTests
{
    [Fact]
    public async Task Checkout_returns_order_id_and_public_key()
    {
        var client = new ScriptedRazorpayClient();
        var provider = Provider(client);

        var result = await provider.CreateCheckoutAsync(Checkout());

        Assert.Equal(PaymentProviderStatus.Succeeded, result.Status);
        Assert.Equal("order_1", result.ProviderOrderId);
        Assert.Equal("rzp_test_key", result.ClientKey);
        Assert.Null(result.ProviderPaymentId);
        Assert.Equal(1, client.OrdersCreated);
    }

    [Fact]
    public async Task Payment_link_returns_short_url_and_link_id()
    {
        var companyId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var client = new ScriptedRazorpayClient();
        var provider = Provider(client);

        var result = await provider.CreatePaymentLinkAsync(new PaymentLinkRequest(
            companyId,
            98m,
            "INR",
            "plink:test",
            invoiceId,
            "2026-08"));

        Assert.Equal(PaymentProviderStatus.Succeeded, result.Status);
        Assert.Equal("https://rzp.io/i/test", result.CheckoutUrl);
        Assert.Equal("plink_1", result.ProviderPaymentLinkId);
        Assert.Equal(1, client.PaymentLinksCreated);
    }

    [Fact]
    public async Task Payment_link_replays_for_a_duplicate_key()
    {
        var provider = Provider(new ScriptedRazorpayClient());
        var request = new PaymentLinkRequest(
            Guid.NewGuid(),
            98m,
            "INR",
            "plink:same",
            Guid.NewGuid(),
            "2026-08");
        var first = await provider.CreatePaymentLinkAsync(request);
        var replay = await provider.CreatePaymentLinkAsync(request);

        Assert.Equal(PaymentProviderStatus.Succeeded, first.Status);
        Assert.Equal(PaymentProviderStatus.Duplicate, replay.Status);
        Assert.Equal(first.CheckoutUrl, replay.CheckoutUrl);
        Assert.Equal(first.ProviderPaymentLinkId, replay.ProviderPaymentLinkId);
    }

    [Fact]
    public async Task Webhook_payment_link_paid_succeeds_with_link_and_order_ids()
    {
        var companyId = Guid.NewGuid();
        var client = new ScriptedRazorpayClient
        {
            Payment = new RazorpayPaymentRecord("pay_1", "order_1", "captured", 9800, "INR", Notes(companyId))
        };
        var body =
            "{\"id\":\"evt_plink\",\"event\":\"payment_link.paid\",\"payload\":{"
            + "\"payment_link\":{\"entity\":{\"id\":\"plink_1\",\"order_id\":\"order_1\",\"amount\":9800,"
            + "\"currency\":\"INR\",\"notes\":{\"companyId\":\"" + companyId.ToString("D") + "\"}}},"
            + "\"payment\":{\"entity\":{\"id\":\"pay_1\",\"order_id\":\"order_1\",\"amount\":9800,"
            + "\"currency\":\"INR\",\"status\":\"captured\",\"notes\":{\"companyId\":\""
            + companyId.ToString("D") + "\"}}}}}";
        var webhook = await Provider(client).HandleWebhookAsync(
            new PaymentWebhookRequest(body, "sig", Header()));

        Assert.Equal(PaymentWebhookEventType.PaymentSucceeded, webhook.Type);
        Assert.Equal(PaymentProviderStatus.Succeeded, webhook.Status);
        Assert.Equal("plink_1", webhook.ProviderPaymentLinkId);
        Assert.Equal("order_1", webhook.ProviderOrderId);
        Assert.Equal("pay_1", webhook.ProviderPaymentId);
        Assert.Equal(98m, webhook.Amount);
        Assert.Equal(companyId, webhook.CompanyId);
    }

    [Fact]
    public async Task Checkout_replays_the_same_order_for_a_duplicate_key()
    {
        var provider = Provider(new ScriptedRazorpayClient());
        var first = await provider.CreateCheckoutAsync(Checkout("same"));
        var replay = await provider.CreateCheckoutAsync(Checkout("same"));

        Assert.Equal(PaymentProviderStatus.Succeeded, first.Status);
        Assert.Equal(PaymentProviderStatus.Duplicate, replay.Status);
        Assert.Equal(first.ProviderOrderId, replay.ProviderOrderId);
    }

    [Fact]
    public async Task Verify_requires_a_valid_signature_then_a_captured_payment()
    {
        var client = new ScriptedRazorpayClient
        {
            Payment = new RazorpayPaymentRecord("pay_1", "order_1", "captured", 49900, "INR", Notes())
        };
        var provider = Provider(client);

        var verified = await provider.VerifyPaymentAsync(new PaymentVerificationRequest(
            "pay_1",
            "sig",
            ProviderOrderId: "order_1"));
        Assert.Equal(PaymentProviderStatus.Succeeded, verified.Status);
        Assert.Equal(499m, verified.Amount);
        Assert.Equal("INR", verified.Currency);

        client.PaymentSignatureValid = false;
        var rejected = await provider.VerifyPaymentAsync(new PaymentVerificationRequest(
            "pay_1",
            "bad",
            ProviderOrderId: "order_1"));
        Assert.Equal(PaymentProviderStatus.Failed, rejected.Status);
    }

    [Fact]
    public async Task Authorized_payment_is_not_succeeded()
    {
        var client = new ScriptedRazorpayClient
        {
            Payment = new RazorpayPaymentRecord("pay_1", "order_1", "authorized", 49900, "INR", Notes())
        };
        var verified = await Provider(client).VerifyPaymentAsync(new PaymentVerificationRequest(
            "pay_1",
            "sig",
            ProviderOrderId: "order_1"));
        Assert.Equal(PaymentProviderStatus.Failed, verified.Status);
    }

    [Fact]
    public async Task Status_maps_captured_failed_and_authorized()
    {
        var client = new ScriptedRazorpayClient
        {
            Payment = new RazorpayPaymentRecord("pay_1", "order_1", "captured", 49900, "INR", Notes())
        };
        var provider = Provider(client);
        Assert.Equal(
            PaymentLifecycleStatus.Succeeded,
            (await provider.GetPaymentStatusAsync(new PaymentStatusRequest("pay_1"))).PaymentStatus);

        client.Payment = client.Payment with { Status = "failed" };
        Assert.Equal(
            PaymentLifecycleStatus.Failed,
            (await provider.GetPaymentStatusAsync(new PaymentStatusRequest("pay_1"))).PaymentStatus);

        client.Payment = client.Payment with { Status = "authorized" };
        Assert.Equal(
            PaymentLifecycleStatus.Pending,
            (await provider.GetPaymentStatusAsync(new PaymentStatusRequest("pay_1"))).PaymentStatus);
    }

    [Fact]
    public async Task Recurring_create_verify_and_cancel_use_the_client()
    {
        var client = new ScriptedRazorpayClient
        {
            Payment = new RazorpayPaymentRecord(
                "pay_1",
                null,
                "captured",
                49900,
                "INR",
                Notes(),
                "sub_1")
        };
        var provider = Provider(client);
        var created = await provider.CreateRecurringAsync(new PaymentRecurringRequest(
            Guid.NewGuid(),
            "starter",
            "INR",
            499m,
            "recur-1",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddMonths(1)));
        Assert.Equal("sub_1", created.ProviderSubscriptionId);

        var verified = await provider.VerifyRecurringAsync(
            new PaymentRecurringVerificationRequest("sub_1", "pay_1", "sig"));
        Assert.Equal(PaymentProviderStatus.Succeeded, verified.Status);
        Assert.Equal("sub_1", verified.ProviderSubscriptionId);

        var cancelled = await provider.CancelRecurringAsync(new PaymentCancelRecurringRequest("sub_1", "cancel-1"));
        Assert.Equal(PaymentProviderStatus.Succeeded, cancelled.Status);
        Assert.Equal(1, client.SubscriptionsCancelled);
    }

    [Fact]
    public async Task Webhook_payment_captured_fetches_and_succeeds()
    {
        var companyId = Guid.NewGuid();
        var client = new ScriptedRazorpayClient
        {
            Payment = new RazorpayPaymentRecord("pay_1", "order_1", "captured", 49900, "INR", Notes(companyId))
        };
        var webhook = await Provider(client).HandleWebhookAsync(Webhook(
            "payment.captured",
            "pay_1",
            "order_1",
            companyId));

        Assert.Equal(PaymentWebhookEventType.PaymentSucceeded, webhook.Type);
        Assert.Equal(PaymentProviderStatus.Succeeded, webhook.Status);
        Assert.Equal("pay_1", webhook.ProviderPaymentId);
        Assert.Equal(499m, webhook.Amount);
        Assert.Equal(companyId, webhook.CompanyId);
    }

    [Theory]
    [InlineData("subscription.activated", PaymentWebhookEventType.SubscriptionActivated)]
    [InlineData("subscription.paused", PaymentWebhookEventType.SubscriptionPaused)]
    [InlineData("subscription.halted", PaymentWebhookEventType.SubscriptionPaused)]
    [InlineData("subscription.resumed", PaymentWebhookEventType.SubscriptionResumed)]
    [InlineData("subscription.cancelled", PaymentWebhookEventType.RecurringCancelled)]
    [InlineData("refund.processed", PaymentWebhookEventType.Refunded)]
    [InlineData("payment.refunded", PaymentWebhookEventType.Refunded)]
    [InlineData("payment.failed", PaymentWebhookEventType.PaymentFailed)]
    public async Task Webhook_maps_lifecycle_events(string eventName, PaymentWebhookEventType expected)
    {
        var webhook = await Provider(new ScriptedRazorpayClient()).HandleWebhookAsync(
            new PaymentWebhookRequest(
                "{\"id\":\"evt_map\",\"event\":\"" + eventName + "\"}",
                "sig",
                Header()));
        Assert.Equal(expected, webhook.Type);
        Assert.Equal(PaymentProviderStatus.Succeeded, webhook.Status);
    }

    [Fact]
    public async Task Webhook_authorized_is_ignored()
    {
        var webhook = await Provider(new ScriptedRazorpayClient()).HandleWebhookAsync(
            new PaymentWebhookRequest(
                """{"id":"evt_auth","event":"payment.authorized"}""",
                "sig",
                Header()));
        Assert.Equal(PaymentWebhookEventType.Ignored, webhook.Type);
    }

    [Fact]
    public async Task Bad_webhook_signature_is_failed_and_ignored()
    {
        var client = new ScriptedRazorpayClient { WebhookSignatureValid = false };
        var webhook = await Provider(client).HandleWebhookAsync(Webhook(
            "payment.captured",
            "pay_1",
            "order_1",
            Guid.NewGuid()));
        Assert.Equal(PaymentWebhookEventType.Ignored, webhook.Type);
        Assert.Equal(PaymentProviderStatus.Failed, webhook.Status);
    }

    [Fact]
    public async Task Timeout_and_api_errors_map_to_statuses()
    {
        var timeout = new ScriptedRazorpayClient
        {
            Throw = new RazorpayClientException("Timeout", "timed out")
        };
        Assert.Equal(
            PaymentProviderStatus.Timeout,
            (await Provider(timeout).CreateCheckoutAsync(Checkout())).Status);

        var down = new ScriptedRazorpayClient
        {
            Throw = new RazorpayClientException("Unavailable", "down")
        };
        Assert.Equal(
            PaymentProviderStatus.Unavailable,
            (await Provider(down).CreateCheckoutAsync(Checkout())).Status);
    }

    private static RazorpayPaymentProvider Provider(IRazorpayClient client) =>
        new(
            new RazorpayOptions
            {
                KeyId = "rzp_test_key",
                KeySecret = "secret",
                WebhookSecret = "whsec"
            },
            client);

    private static PaymentCheckoutRequest Checkout(string key = "checkout-1") =>
        new(Guid.NewGuid(), 499m, "INR", key, InvoiceId: Guid.NewGuid());

    private static IReadOnlyDictionary<string, string> Notes(Guid? companyId = null) =>
        new Dictionary<string, string> { ["companyId"] = (companyId ?? Guid.NewGuid()).ToString("D") };

    private static IReadOnlyDictionary<string, string> Header() =>
        new Dictionary<string, string> { [RazorpayPaymentProvider.SignatureHeader] = "sig" };

    private static PaymentWebhookRequest Webhook(
        string eventName,
        string paymentId,
        string orderId,
        Guid companyId) =>
        new(
            "{\"id\":\"evt_1\",\"event\":\"" + eventName
            + "\",\"payload\":{\"payment\":{\"entity\":{\"id\":\"" + paymentId
            + "\",\"order_id\":\"" + orderId
            + "\",\"amount\":49900,\"currency\":\"INR\",\"status\":\"captured\",\"notes\":{\"companyId\":\""
            + companyId.ToString("D") + "\"}}}}}",
            "sig",
            Header());
}
