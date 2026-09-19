using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Billing;
using MiniPayroll.Domain.Billing.Payments;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Payroll;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Payments;
using MiniPayroll.Infrastructure.Payments.Razorpay;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public sealed class PaymentLinkBillingTests
{
    private static readonly DateTimeOffset Activated = new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly TimeProvider Clock = new FrozenTimeProvider(new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Start_payment_link_issues_invoice_and_returns_url()
    {
        var fixture = await FixtureAsync();
        var result = await fixture.Payments.StartPaymentLinkAsync(fixture.Company.Id, "2026-08");

        Assert.Equal(PaymentReconciliationStatus.Success, result.Status);
        Assert.Equal("https://rzp.io/i/test", result.CheckoutUrl);
        Assert.Equal("plink_1", result.ProviderPaymentLinkId);
        Assert.Equal(49m, result.Amount);
        Assert.Equal("2026-08", result.BillingPeriod);
        Assert.NotNull(result.InvoiceId);

        await using var db = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        var invoice = await db.Invoices.SingleAsync();
        Assert.Equal(InvoiceStatus.PaymentPending, invoice.Status);
        var intent = await db.PaymentIntents.SingleAsync();
        Assert.Equal("plink_1", intent.ProviderPaymentLinkId);
        Assert.Equal("https://rzp.io/i/test", intent.CheckoutUrl);
        Assert.Equal(PaymentIntentStatus.Created, intent.Status);
    }

    [Fact]
    public async Task Replay_returns_the_same_link_for_the_same_period()
    {
        var fixture = await FixtureAsync();
        var first = await fixture.Payments.StartPaymentLinkAsync(fixture.Company.Id, "2026-08");
        var replay = await fixture.Payments.StartPaymentLinkAsync(fixture.Company.Id, "2026-08");

        Assert.Equal(PaymentReconciliationStatus.Success, first.Status);
        Assert.Equal(PaymentReconciliationStatus.Success, replay.Status);
        Assert.Equal(first.CheckoutUrl, replay.CheckoutUrl);
        Assert.Equal(first.ProviderPaymentLinkId, replay.ProviderPaymentLinkId);
        Assert.Equal(1, fixture.Client.PaymentLinksCreated);
    }

    [Fact]
    public async Task Remaining_zero_is_rejected()
    {
        var fixture = await FixtureAsync();
        Assert.Equal(
            BillingStatusCode.Success,
            (await fixture.Billing.RecordPaymentAsync(
                fixture.Company.Id,
                new RecordPaymentRequest("2026-08", 49m, Activated, BillingCalculator.ModeUpi, null))).Status);

        var result = await fixture.Payments.StartPaymentLinkAsync(fixture.Company.Id, "2026-08");
        Assert.Equal(PaymentReconciliationStatus.InvalidInput, result.Status);
    }

    [Fact]
    public async Task Company_admin_cannot_create_payment_links()
    {
        var fixture = await FixtureAsync();
        var admin = new StaticTenantContext
        {
            UserId = Guid.NewGuid(),
            CompanyId = fixture.Company.Id,
            IsSuperadmin = false
        };
        await using var db = TestDb.Create(admin, fixture.Database);
        var payments = PaymentsFor(fixture.Database, fixture.Client, admin);

        Assert.Equal(
            PaymentReconciliationStatus.Forbidden,
            (await payments.StartPaymentLinkAsync(fixture.Company.Id, "2026-08")).Status);
    }

    [Fact]
    public async Task Payment_link_paid_webhook_settles_invoice_and_clears_hold()
    {
        var fixture = await FixtureAsync();
        var link = await fixture.Payments.StartPaymentLinkAsync(fixture.Company.Id, "2026-08");
        Assert.Equal(PaymentReconciliationStatus.Success, link.Status);

        fixture.Client.Payment = new RazorpayPaymentRecord(
            "pay_1",
            "order_1",
            "captured",
            4900,
            "INR",
            new Dictionary<string, string> { ["companyId"] = fixture.Company.Id.ToString("D") });

        var body =
            "{\"id\":\"evt_plink\",\"event\":\"payment_link.paid\",\"payload\":{"
            + "\"payment_link\":{\"entity\":{\"id\":\"plink_1\",\"order_id\":\"order_1\",\"amount\":4900,"
            + "\"currency\":\"INR\",\"notes\":{\"companyId\":\"" + fixture.Company.Id.ToString("D") + "\"}}},"
            + "\"payment\":{\"entity\":{\"id\":\"pay_1\",\"order_id\":\"order_1\",\"amount\":4900,"
            + "\"currency\":\"INR\",\"status\":\"captured\",\"notes\":{\"companyId\":\""
            + fixture.Company.Id.ToString("D") + "\"}}}}}";

        var webhook = await fixture.Payments.HandleWebhookAsync(
            body,
            "sig",
            new Dictionary<string, string> { [RazorpayPaymentProvider.SignatureHeader] = "sig" });

        Assert.Equal(PaymentReconciliationStatus.Success, webhook.Status);

        await using var db = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        Assert.Equal(InvoiceStatus.Paid, (await db.Invoices.SingleAsync()).Status);
        Assert.Equal(PaymentIntentStatus.Verified, (await db.PaymentIntents.SingleAsync()).Status);
        Assert.True(await db.Payments.AnyAsync(item => item.BillingPeriod == "2026-08"));

        var hold = await fixture.Billing.GetPriorPeriodHoldAsync(
            fixture.Company.Id,
            new PayrollPeriod(2026, 9));
        Assert.False(hold.IsHeld);
    }

    [Fact]
    public async Task Billing_get_includes_open_payment_link_url()
    {
        var fixture = await FixtureAsync();
        Assert.Equal(
            PaymentReconciliationStatus.Success,
            (await fixture.Payments.StartPaymentLinkAsync(fixture.Company.Id, "2026-08")).Status);

        var billing = await fixture.Billing.GetAsync(fixture.Company.Id);
        var august = Assert.Single(billing.Billing!.Periods, period => period.BillingPeriod == "2026-08");
        Assert.Equal("https://rzp.io/i/test", august.PaymentLinkUrl);
    }

    private sealed record Fixture(
        string Database,
        Company Company,
        BillingService Billing,
        PaymentReconciliationService Payments,
        ScriptedRazorpayClient Client);

    private static async Task<Fixture> FixtureAsync()
    {
        var database = $"plink-{Guid.NewGuid():N}";
        var company = await SeedCompanyAsync(database);
        await SeedEmployeeAsync(database, company.Id);
        var tenant = new StaticTenantContext
        {
            UserId = Guid.NewGuid(),
            IsSuperadmin = true
        };
        var client = new ScriptedRazorpayClient
        {
            PaymentLink = new RazorpayPaymentLinkRecord(
                "plink_1",
                "https://rzp.io/i/test",
                4900,
                "INR",
                new Dictionary<string, string>())
        };
        var db = TestDb.Create(tenant, database);
        return new Fixture(
            database,
            company,
            new BillingService(db, tenant, Clock),
            PaymentsFor(database, client, tenant),
            client);
    }

    private static PaymentReconciliationService PaymentsFor(
        string database,
        ScriptedRazorpayClient client,
        ITenantContext tenant)
    {
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
            new InvoiceService(db, tenant, Clock),
            new SubscriptionLifecycleService(db, tenant, Clock),
            Clock);
    }

    private static async Task<Company> SeedCompanyAsync(string database)
    {
        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = "Acme",
            ContactEmail = $"{Guid.NewGuid():N}@example.com",
            IsSetupComplete = true,
            SetupStep = CompanySetupStep.Complete,
            CreatedAt = Activated,
            ActivatedAt = Activated
        };
        var plan = new Plan
        {
            Id = Guid.NewGuid(),
            Name = $"Basic-{Guid.NewGuid():N}",
            PricePerEmployee = 49m,
            DefaultEmployeeLimit = 9
        };
        company.Subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Active,
            EmployeeLimit = 9,
            GracePeriodDays = 7
        };
        await using var db = TestDb.Create(NullTenantContext.Instance, database);
        db.Plans.Add(plan);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return company;
    }

    private static async Task SeedEmployeeAsync(string database, Guid companyId)
    {
        await using var db = TestDb.Create(NullTenantContext.Instance, database);
        db.Employees.Add(new Employee
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            EmployeeCode = $"EMP-{Guid.NewGuid():N}",
            FullName = "Ada Lovelace",
            Phone = "9876543210",
            Email = "ada@example.com",
            AddressLine1 = "Main Road",
            City = "Pune",
            State = "Maharashtra",
            PostalCode = "411001",
            Designation = "Engineer",
            JoiningDate = new DateOnly(2026, 1, 1),
            BankName = "HDFC Bank",
            BankAccountNumber = "123456789012",
            Ifsc = "HDFC0001234",
            Status = EmployeeStatus.Active,
            CreatedAt = Activated
        });
        await db.SaveChangesAsync();
    }

    private sealed class FrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
