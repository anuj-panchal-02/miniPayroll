using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Billing.Payments;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Subscriptions;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Payments;
using MiniPayroll.Infrastructure.Payments.Razorpay;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public sealed class PaymentReconciliationServiceTests
{
    private static readonly DateTimeOffset Jan2026 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset EndJan2026 = new(2026, 1, 31, 23, 59, 59, TimeSpan.Zero);

    [Fact]
    public async Task Verify_after_checkout_applies_the_invoice_and_activates()
    {
        var fixture = await SeedAsync(SubscriptionStatus.Trialing, invoice: true);
        var checkout = await fixture.Payments.StartCheckoutAsync(
            fixture.CompanyId,
            499m,
            "INR",
            "pay-1",
            fixture.InvoiceId);
        Assert.Equal(PaymentReconciliationStatus.Success, checkout.Status);

        var verified = await fixture.Payments.VerifyAsync(
            fixture.CompanyId,
            checkout.ProviderOrderId,
            "pay_1",
            "sig");

        Assert.Equal(PaymentReconciliationStatus.Success, verified.Status);
        await using var db = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        var company = await db.Companies.Include(item => item.Subscription).SingleAsync();
        var invoice = await db.Invoices.SingleAsync();
        var intent = await db.PaymentIntents.SingleAsync();
        Assert.Equal(SubscriptionStatus.Active, company.Subscription!.Status);
        Assert.Equal(InvoiceStatus.Paid, invoice.Status);
        Assert.Equal(PaymentIntentStatus.Verified, intent.Status);
        Assert.Contains(
            await db.SubscriptionEvents.Select(item => item.Type).ToListAsync(),
            type => type == SubscriptionEventType.PaymentSucceeded);
    }

    [Fact]
    public async Task Bad_signature_does_not_activate()
    {
        var fixture = await SeedAsync(SubscriptionStatus.Trialing, invoice: true, validSignature: false);
        var checkout = await fixture.Payments.StartCheckoutAsync(
            fixture.CompanyId,
            499m,
            "INR",
            "pay-bad",
            fixture.InvoiceId);
        var verified = await fixture.Payments.VerifyAsync(
            fixture.CompanyId,
            checkout.ProviderOrderId,
            "pay_1",
            "bad");

        Assert.Equal(PaymentReconciliationStatus.Failed, verified.Status);
        await AssertStillTrialingAsync(fixture);
    }

    [Fact]
    public async Task Amount_mismatch_does_not_activate()
    {
        var fixture = await SeedAsync(SubscriptionStatus.Trialing, invoice: true, paymentPaise: 100);
        var checkout = await fixture.Payments.StartCheckoutAsync(
            fixture.CompanyId,
            499m,
            "INR",
            "pay-amt",
            fixture.InvoiceId);
        var verified = await fixture.Payments.VerifyAsync(
            fixture.CompanyId,
            checkout.ProviderOrderId,
            "pay_1",
            "sig");

        Assert.Equal(PaymentReconciliationStatus.Failed, verified.Status);
        await AssertStillTrialingAsync(fixture);
    }

    [Fact]
    public async Task Currency_mismatch_does_not_activate()
    {
        var fixture = await SeedAsync(SubscriptionStatus.Trialing, invoice: true, paymentCurrency: "USD");
        var checkout = await fixture.Payments.StartCheckoutAsync(
            fixture.CompanyId,
            499m,
            "INR",
            "pay-cur",
            fixture.InvoiceId);
        var verified = await fixture.Payments.VerifyAsync(
            fixture.CompanyId,
            checkout.ProviderOrderId,
            "pay_1",
            "sig");

        Assert.Equal(PaymentReconciliationStatus.Failed, verified.Status);
        await AssertStillTrialingAsync(fixture);
    }

    [Fact]
    public async Task Company_mismatch_does_not_activate()
    {
        var fixture = await SeedAsync(
            SubscriptionStatus.Trialing,
            invoice: true,
            paymentCompanyId: Guid.NewGuid());
        var checkout = await fixture.Payments.StartCheckoutAsync(
            fixture.CompanyId,
            499m,
            "INR",
            "pay-co",
            fixture.InvoiceId);
        var verified = await fixture.Payments.VerifyAsync(
            fixture.CompanyId,
            checkout.ProviderOrderId,
            "pay_1",
            "sig");

        Assert.Equal(PaymentReconciliationStatus.Failed, verified.Status);
        await AssertStillTrialingAsync(fixture);
    }

    [Fact]
    public async Task Client_success_without_verify_does_not_activate()
    {
        var fixture = await SeedAsync(SubscriptionStatus.Trialing);
        await fixture.Payments.StartCheckoutAsync(fixture.CompanyId, 499m, "INR", "pay-client", null);
        await AssertStillTrialingAsync(fixture);
    }

    [Fact]
    public async Task Duplicate_checkout_key_does_not_activate()
    {
        var fixture = await SeedAsync(SubscriptionStatus.Trialing);
        var first = await fixture.Payments.StartCheckoutAsync(fixture.CompanyId, 499m, "INR", "dup", null);
        var replay = await fixture.Payments.StartCheckoutAsync(fixture.CompanyId, 499m, "INR", "dup", null);

        Assert.Equal(PaymentReconciliationStatus.Success, first.Status);
        Assert.Equal(PaymentReconciliationStatus.Duplicate, replay.Status);
        Assert.Equal(first.ProviderOrderId, replay.ProviderOrderId);
        await AssertStillTrialingAsync(fixture);
    }

    [Fact]
    public async Task Webhook_captured_activates_once_and_replay_is_idempotent()
    {
        var fixture = await SeedAsync(SubscriptionStatus.Trialing, invoice: true);
        var checkout = await fixture.Payments.StartCheckoutAsync(
            fixture.CompanyId,
            499m,
            "INR",
            "pay-wh",
            fixture.InvoiceId);
        var body = WebhookBody("evt_1", checkout.ProviderOrderId!, fixture.CompanyId);

        var first = await fixture.Payments.HandleWebhookAsync(
            body,
            "sig",
            new Dictionary<string, string> { [RazorpayPaymentProvider.SignatureHeader] = "sig" });
        var replay = await fixture.Payments.HandleWebhookAsync(
            body,
            "sig",
            new Dictionary<string, string> { [RazorpayPaymentProvider.SignatureHeader] = "sig" });

        Assert.Equal(PaymentReconciliationStatus.Success, first.Status);
        Assert.Equal(PaymentReconciliationStatus.Duplicate, replay.Status);
        await using var db = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        Assert.Equal(SubscriptionStatus.Active, (await db.Companies.Include(item => item.Subscription).SingleAsync()).Subscription!.Status);
        Assert.Equal(InvoiceStatus.Paid, (await db.Invoices.SingleAsync()).Status);
        Assert.Single(await db.PaymentProviderEvents.ToListAsync());
        var stored = await db.PaymentProviderEvents.SingleAsync();
        Assert.Equal(PaymentWebhookProcessingStatus.Processed, stored.ProcessingStatus);
        Assert.False(string.IsNullOrWhiteSpace(stored.PayloadHash));
        Assert.Equal(1, await db.SubscriptionEvents.CountAsync(item => item.Type == SubscriptionEventType.Activated));
        Assert.Equal(1, await db.SubscriptionEvents.CountAsync(item => item.Type == SubscriptionEventType.PaymentSucceeded));
    }

    [Fact]
    public async Task Invalid_webhook_signature_persists_nothing()
    {
        var fixture = await SeedAsync(SubscriptionStatus.Trialing, invoice: true, validSignature: false);
        var checkout = await fixture.Payments.StartCheckoutAsync(
            fixture.CompanyId, 499m, "INR", "pay-sig", fixture.InvoiceId);

        var result = await fixture.Payments.HandleWebhookAsync(
            WebhookBody("evt_sig", checkout.ProviderOrderId!, fixture.CompanyId),
            "bad",
            Header());

        Assert.Equal(PaymentReconciliationStatus.Failed, result.Status);
        await AssertStillTrialingAsync(fixture);
        await using var db = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        Assert.Empty(await db.PaymentProviderEvents.ToListAsync());
    }

    [Fact]
    public async Task Out_of_order_failure_after_capture_keeps_the_paid_subscription()
    {
        var fixture = await SeedAsync(SubscriptionStatus.Trialing, invoice: true);
        var checkout = await fixture.Payments.StartCheckoutAsync(
            fixture.CompanyId, 499m, "INR", "pay-oo", fixture.InvoiceId);
        Assert.Equal(
            PaymentReconciliationStatus.Success,
            (await fixture.Payments.HandleWebhookAsync(
                WebhookBody("evt_cap", checkout.ProviderOrderId!, fixture.CompanyId),
                "sig",
                Header())).Status);

        var failed = await fixture.Payments.HandleWebhookAsync(
            EventBody("evt_fail", "payment.failed", checkout.ProviderOrderId!, fixture.CompanyId),
            "sig",
            Header());

        Assert.Equal(PaymentReconciliationStatus.Success, failed.Status);
        await using var db = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        Assert.Equal(SubscriptionStatus.Active, (await db.Companies.Include(item => item.Subscription).SingleAsync()).Subscription!.Status);
        Assert.Equal(InvoiceStatus.Paid, (await db.Invoices.SingleAsync()).Status);
        Assert.Equal(PaymentIntentStatus.Verified, (await db.PaymentIntents.SingleAsync()).Status);
    }

    [Fact]
    public async Task Failed_processing_leaves_the_event_retryable_then_succeeds_once()
    {
        var fixture = await SeedAsync(SubscriptionStatus.Trialing, invoice: true, issueInvoice: false);
        var checkout = await fixture.Payments.StartCheckoutAsync(
            fixture.CompanyId, 499m, "INR", "pay-retry", fixture.InvoiceId);
        var body = WebhookBody("evt_retry", checkout.ProviderOrderId!, fixture.CompanyId);

        var first = await fixture.Payments.HandleWebhookAsync(body, "sig", Header());
        Assert.Equal(PaymentReconciliationStatus.Retryable, first.Status);
        await using (var reader = TestDb.Create(NullTenantContext.Instance, fixture.Database))
        {
            Assert.Equal(PaymentWebhookProcessingStatus.Failed, (await reader.PaymentProviderEvents.SingleAsync()).ProcessingStatus);
            Assert.Equal(PaymentIntentStatus.Created, (await reader.PaymentIntents.SingleAsync()).Status);
            Assert.Equal(SubscriptionStatus.Trialing, (await reader.Companies.Include(item => item.Subscription).SingleAsync()).Subscription!.Status);
        }

        await using (var invoiceDb = TestDb.Create(NullTenantContext.Instance, fixture.Database))
        {
            await new InvoiceService(invoiceDb, NullTenantContext.Instance, new FrozenTimeProvider(Jan2026))
                .IssueAsync(fixture.CompanyId, fixture.InvoiceId!.Value);
        }

        var retry = await PaymentsFor(fixture.Database, fixture.Client)
            .HandleWebhookAsync(body, "sig", Header());
        Assert.Equal(PaymentReconciliationStatus.Success, retry.Status);
        await using var db = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        Assert.Equal(PaymentWebhookProcessingStatus.Processed, (await db.PaymentProviderEvents.SingleAsync()).ProcessingStatus);
        Assert.Equal(SubscriptionStatus.Active, (await db.Companies.Include(item => item.Subscription).SingleAsync()).Subscription!.Status);
        Assert.Equal(InvoiceStatus.Paid, (await db.Invoices.SingleAsync()).Status);
        Assert.Equal(1, await db.SubscriptionEvents.CountAsync(item => item.Type == SubscriptionEventType.Activated));
    }

    [Fact]
    public async Task Unknown_webhook_is_ignored_without_domain_writes()
    {
        var fixture = await SeedAsync(SubscriptionStatus.Trialing, invoice: true);
        var checkout = await fixture.Payments.StartCheckoutAsync(
            fixture.CompanyId, 499m, "INR", "pay-unk", fixture.InvoiceId);

        var result = await fixture.Payments.HandleWebhookAsync(
            EventBody("evt_unk", "payment.authorized", checkout.ProviderOrderId!, fixture.CompanyId),
            "sig",
            Header());

        Assert.Equal(PaymentReconciliationStatus.Ignored, result.Status);
        await AssertStillTrialingAsync(fixture);
        await using var db = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        var stored = Assert.Single(await db.PaymentProviderEvents.ToListAsync());
        Assert.Equal(PaymentWebhookProcessingStatus.Ignored, stored.ProcessingStatus);
        Assert.Equal(PaymentWebhookEventType.Ignored, stored.EventType);
    }

    [Fact]
    public async Task Already_paid_invoice_and_active_subscription_are_no_ops()
    {
        var fixture = await SeedAsync(SubscriptionStatus.Active, invoice: true);
        var checkout = await fixture.Payments.StartCheckoutAsync(
            fixture.CompanyId, 499m, "INR", "pay-paid", fixture.InvoiceId);
        Assert.Equal(
            PaymentReconciliationStatus.Success,
            (await fixture.Payments.VerifyAsync(fixture.CompanyId, checkout.ProviderOrderId, "pay_1", "sig")).Status);

        var webhook = await fixture.Payments.HandleWebhookAsync(
            WebhookBody("evt_paid", checkout.ProviderOrderId!, fixture.CompanyId),
            "sig",
            Header());

        Assert.Equal(PaymentReconciliationStatus.Success, webhook.Status);
        await using var db = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        Assert.Equal(InvoiceStatus.Paid, (await db.Invoices.SingleAsync()).Status);
        Assert.Equal(0, await db.SubscriptionEvents.CountAsync(item => item.Type == SubscriptionEventType.Activated));
        Assert.Equal(0, await db.SubscriptionEvents.CountAsync(item => item.Type == SubscriptionEventType.PaymentSucceeded));
    }

    [Fact]
    public async Task Pause_cancel_resume_and_refund_are_idempotent_no_ops_when_already_applied()
    {
        var fixture = await SeedAsync(SubscriptionStatus.Active, invoice: true);
        var checkout = await fixture.Payments.StartCheckoutAsync(
            fixture.CompanyId, 499m, "INR", "pay-life", fixture.InvoiceId);
        await fixture.Payments.HandleWebhookAsync(
            WebhookBody("evt_life", checkout.ProviderOrderId!, fixture.CompanyId),
            "sig",
            Header());

        var paused = await fixture.Payments.HandleWebhookAsync(
            EventBody("evt_pause", "subscription.paused", checkout.ProviderOrderId!, fixture.CompanyId, "sub_1"),
            "sig",
            Header());
        var pausedAgain = await fixture.Payments.HandleWebhookAsync(
            EventBody("evt_pause", "subscription.paused", checkout.ProviderOrderId!, fixture.CompanyId, "sub_1"),
            "sig",
            Header());
        Assert.Equal(PaymentReconciliationStatus.Success, paused.Status);
        Assert.Equal(PaymentReconciliationStatus.Duplicate, pausedAgain.Status);

        var resumed = await fixture.Payments.HandleWebhookAsync(
            EventBody("evt_resume", "subscription.resumed", checkout.ProviderOrderId!, fixture.CompanyId, "sub_1"),
            "sig",
            Header());
        Assert.Equal(PaymentReconciliationStatus.Success, resumed.Status);

        var refunded = await fixture.Payments.HandleWebhookAsync(
            EventBody("evt_refund", "refund.processed", checkout.ProviderOrderId!, fixture.CompanyId),
            "sig",
            Header());
        var cancelled = await fixture.Payments.HandleWebhookAsync(
            EventBody("evt_cancel", "subscription.cancelled", checkout.ProviderOrderId!, fixture.CompanyId, "sub_1"),
            "sig",
            Header());

        Assert.Equal(PaymentReconciliationStatus.Success, refunded.Status);
        Assert.Equal(PaymentReconciliationStatus.Success, cancelled.Status);
        await using var db = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        Assert.Equal(InvoiceStatus.Refunded, (await db.Invoices.SingleAsync()).Status);
        Assert.Equal(SubscriptionStatus.Cancelled, (await db.Companies.Include(item => item.Subscription).SingleAsync()).Subscription!.Status);
        Assert.Equal(5, await db.PaymentProviderEvents.CountAsync(item => item.ProcessingStatus == PaymentWebhookProcessingStatus.Processed));
    }

    [Fact]
    public async Task Recurring_cancel_uses_the_provider()
    {
        var fixture = await SeedAsync(SubscriptionStatus.Active);
        var created = await fixture.Payments.StartRecurringAsync(
            fixture.CompanyId,
            "starter",
            "INR",
            499m,
            "recur-1",
            Jan2026,
            EndJan2026);
        Assert.Equal(PaymentReconciliationStatus.Success, created.Status);

        var cancelled = await fixture.Payments.CancelRecurringAsync(
            fixture.CompanyId,
            created.ProviderSubscriptionId!,
            "cancel-1");
        Assert.Equal(PaymentReconciliationStatus.Success, cancelled.Status);
        Assert.Equal(1, fixture.Client.SubscriptionsCancelled);
    }

    private static async Task AssertStillTrialingAsync(Fixture fixture)
    {
        await using var db = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        var company = await db.Companies.Include(item => item.Subscription).SingleAsync();
        Assert.Equal(SubscriptionStatus.Trialing, company.Subscription!.Status);
        Assert.False(await db.Invoices.AnyAsync(item => item.Status == InvoiceStatus.Paid));
        Assert.DoesNotContain(
            await db.SubscriptionEvents.Select(item => item.Type).ToListAsync(),
            type => type is SubscriptionEventType.Activated or SubscriptionEventType.PaymentSucceeded);
    }

    private sealed record Fixture(
        string Database,
        Guid CompanyId,
        Guid? InvoiceId,
        PaymentReconciliationService Payments,
        ScriptedRazorpayClient Client);

    private static async Task<Fixture> SeedAsync(
        SubscriptionStatus status,
        bool invoice = false,
        bool issueInvoice = true,
        bool validSignature = true,
        int paymentPaise = 49900,
        string paymentCurrency = "INR",
        Guid? paymentCompanyId = null)
    {
        var database = $"rzp-{Guid.NewGuid():N}";
        var companyId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var plan = new Plan
        {
            Id = planId,
            Code = "starter",
            Name = "Starter",
            IsActive = true,
            MaxActiveEmployees = 50,
            PricePerEmployee = 499m,
            DefaultEmployeeLimit = 50
        };
        PlanPricing.Revise(plan, BillingCycle.Monthly, 499m, Jan2026);

        await using (var writer = TestDb.Create(NullTenantContext.Instance, database))
        {
            writer.Plans.Add(plan);
            writer.Companies.Add(new Company
            {
                Id = companyId,
                Name = "Pay Co",
                ContactEmail = $"{companyId:N}@example.com",
                IsSetupComplete = true,
                SetupStep = CompanySetupStep.Complete,
                CreatedAt = Jan2026,
                Subscription = new Subscription
                {
                    Id = subscriptionId,
                    CompanyId = companyId,
                    PlanId = planId,
                    Status = status,
                    BillingCycle = BillingCycle.Monthly,
                    EmployeeLimit = 10,
                    GracePeriodDays = 7,
                    CreatedAt = Jan2026,
                    UpdatedAt = Jan2026
                }
            });
            await writer.SaveChangesAsync();
        }

        Guid? invoiceId = null;
        if (invoice)
        {
            await using var invoiceDb = TestDb.Create(NullTenantContext.Instance, database);
            var invoices = new InvoiceService(
                invoiceDb,
                NullTenantContext.Instance,
                new FrozenTimeProvider(Jan2026));
            var created = await invoices.CreateAsync(companyId, Jan2026, EndJan2026, 1);
            if (issueInvoice)
            {
                await invoices.IssueAsync(companyId, created.Invoice!.Id);
            }

            invoiceId = created.Invoice.Id;
        }

        var notes = new Dictionary<string, string>
        {
            ["companyId"] = (paymentCompanyId ?? companyId).ToString("D")
        };
        var client = new ScriptedRazorpayClient
        {
            PaymentSignatureValid = validSignature,
            WebhookSignatureValid = validSignature,
            Order = new RazorpayOrderRecord("order_1", 49900, "INR", notes),
            Payment = new RazorpayPaymentRecord("pay_1", "order_1", "captured", paymentPaise, paymentCurrency, notes)
        };
        return new Fixture(database, companyId, invoiceId, PaymentsFor(database, client), client);
    }

    private static PaymentReconciliationService PaymentsFor(string database, ScriptedRazorpayClient client)
    {
        var clock = new FrozenTimeProvider(Jan2026);
        var tenant = NullTenantContext.Instance;
        var db = TestDb.Create(tenant, database);
        return new PaymentReconciliationService(
            db,
            tenant,
            new PaymentGatewayService(new RazorpayPaymentProvider(
                new RazorpayOptions
                {
                    KeyId = "rzp_test_key",
                    KeySecret = "secret",
                    WebhookSecret = "whsec"
                },
                client)),
            new InvoiceService(db, tenant, clock),
            new SubscriptionLifecycleService(db, tenant, clock),
            clock);
    }

    private static IReadOnlyDictionary<string, string> Header() =>
        new Dictionary<string, string> { [RazorpayPaymentProvider.SignatureHeader] = "sig" };

    private static string WebhookBody(string eventId, string orderId, Guid companyId) =>
        EventBody(eventId, "payment.captured", orderId, companyId);

    private static string EventBody(
        string eventId,
        string eventName,
        string orderId,
        Guid companyId,
        string? subscriptionId = null) =>
        "{\"id\":\"" + eventId
        + "\",\"event\":\"" + eventName
        + "\",\"payload\":{\"payment\":{\"entity\":{\"id\":\"pay_1\",\"order_id\":\""
        + orderId + "\",\"amount\":49900,\"currency\":\"INR\",\"status\":\"captured\",\"notes\":{\"companyId\":\""
        + companyId.ToString("D") + "\"}}}"
        + (subscriptionId is null
            ? ""
            : ",\"subscription\":{\"entity\":{\"id\":\"" + subscriptionId + "\"}}")
        + "}}";

    private sealed class FrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
