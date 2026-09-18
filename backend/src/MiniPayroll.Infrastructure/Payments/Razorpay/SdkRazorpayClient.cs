using Microsoft.Extensions.Options;
using Razorpay.Api;

namespace MiniPayroll.Infrastructure.Payments.Razorpay;

public sealed class SdkRazorpayClient(IOptions<RazorpayOptions> options) : IRazorpayClient
{
    private readonly RazorpayOptions options = options.Value;
    private RazorpayClient? client;

    public Task<RazorpayOrderRecord> CreateOrderAsync(
        RazorpayOrderCreateRequest request,
        CancellationToken cancellationToken = default) =>
        Run(() =>
        {
            var created = Client().Order.Create(new Dictionary<string, object>
            {
                ["amount"] = request.AmountPaise,
                ["currency"] = request.Currency,
                ["receipt"] = request.Receipt,
                ["notes"] = Notes(request.Notes)
            });
            return MapOrder(created);
        }, cancellationToken);

    public Task<RazorpayOrderRecord> FetchOrderAsync(
        string orderId,
        CancellationToken cancellationToken = default) =>
        Run(() => MapOrder(Client().Order.Fetch(orderId)), cancellationToken);

    public Task<RazorpayPaymentRecord> FetchPaymentAsync(
        string paymentId,
        CancellationToken cancellationToken = default) =>
        Run(() => MapPayment(Client().Payment.Fetch(paymentId)), cancellationToken);

    public Task<RazorpaySubscriptionRecord> CreateSubscriptionAsync(
        RazorpaySubscriptionCreateRequest request,
        CancellationToken cancellationToken = default) =>
        Run(() =>
        {
            var created = Client().Subscription.Create(new Dictionary<string, object>
            {
                ["plan_id"] = request.PlanId,
                ["total_count"] = request.TotalCount,
                ["quantity"] = request.Quantity,
                ["notes"] = Notes(request.Notes)
            });
            return MapSubscription(created);
        }, cancellationToken);

    public Task<RazorpaySubscriptionRecord> FetchSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default) =>
        Run(() => MapSubscription(Client().Subscription.Fetch(subscriptionId)), cancellationToken);

    public Task<RazorpaySubscriptionRecord> CancelSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default) =>
        Run(
            () => MapSubscription(Client().Subscription.Fetch(subscriptionId).Cancel(
                new Dictionary<string, object> { ["cancel_at_cycle_end"] = false })),
            cancellationToken);

    public bool VerifyPaymentSignature(string orderId, string paymentId, string signature)
    {
        try
        {
            EnsureClient();
            Utils.verifyPaymentSignature(new Dictionary<string, string>
            {
                ["razorpay_order_id"] = orderId,
                ["razorpay_payment_id"] = paymentId,
                ["razorpay_signature"] = signature
            });
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool VerifySubscriptionSignature(string subscriptionId, string paymentId, string signature)
    {
        try
        {
            EnsureClient();
            Utils.verifySubscriptionSignature(new Dictionary<string, string>
            {
                ["razorpay_subscription_id"] = subscriptionId,
                ["razorpay_payment_id"] = paymentId,
                ["razorpay_signature"] = signature
            });
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public bool VerifyWebhookSignature(string rawBody, string signature, string webhookSecret)
    {
        try
        {
            Utils.verifyWebhookSignature(rawBody, signature, webhookSecret);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private RazorpayClient Client()
    {
        EnsureClient();
        return client!;
    }

    private void EnsureClient()
    {
        if (!options.IsConfigured)
        {
            throw new RazorpayClientException("Unavailable", "Razorpay is not configured.");
        }

        client ??= new RazorpayClient(options.KeyId, options.KeySecret);
    }

    private static Task<T> Run<T>(Func<T> work, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return Task.FromResult(work());
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException exception)
        {
            throw new RazorpayClientException("Timeout", exception.Message, exception);
        }
        catch (RazorpayClientException)
        {
            throw;
        }
        catch (Exception exception) when (IsTimeout(exception))
        {
            throw new RazorpayClientException("Timeout", exception.Message, exception);
        }
        catch (Exception exception)
        {
            throw new RazorpayClientException("Unavailable", exception.Message, exception);
        }
    }

    private static bool IsTimeout(Exception exception) =>
        exception is TimeoutException
        || exception.GetType().Name.Contains("Timeout", StringComparison.OrdinalIgnoreCase)
        || exception.InnerException is TimeoutException;

    private static Dictionary<string, object> Notes(IReadOnlyDictionary<string, string> notes) =>
        notes.ToDictionary(pair => pair.Key, pair => (object)pair.Value, StringComparer.Ordinal);

    private static RazorpayOrderRecord MapOrder(Order order) =>
        new(
            ReadString(order, "id"),
            ReadPaise(order, "amount"),
            ReadString(order, "currency"),
            ReadNotes(order));

    private static RazorpayPaymentRecord MapPayment(Payment payment) =>
        new(
            ReadString(payment, "id"),
            ReadOptional(payment, "order_id"),
            ReadString(payment, "status"),
            ReadPaise(payment, "amount"),
            ReadString(payment, "currency"),
            ReadNotes(payment),
            ReadOptional(payment, "subscription_id"));

    private static RazorpaySubscriptionRecord MapSubscription(Subscription subscription) =>
        new(ReadString(subscription, "id"), ReadString(subscription, "status"), ReadNotes(subscription));

    private static string ReadString(Entity entity, string key) =>
        ReadOptional(entity, key) ?? string.Empty;

    private static string? ReadOptional(Entity entity, string key)
    {
        try
        {
            return entity[key]?.ToString();
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static int ReadPaise(Entity entity, string key)
    {
        var value = entity[key];
        if (value is null)
        {
            return 0;
        }

        if (value is int paise)
        {
            return paise;
        }

        if (value is long longPaise)
        {
            return (int)longPaise;
        }

        if (value is decimal decimalPaise)
        {
            return (int)decimalPaise;
        }

        return int.TryParse(value.ToString(), out int parsed) ? parsed : 0;
    }

    private static IReadOnlyDictionary<string, string> ReadNotes(Entity entity)
    {
        object? notes;
        try
        {
            notes = entity["notes"];
        }
        catch (Exception)
        {
            return new Dictionary<string, string>();
        }

        if (notes is IDictionary<string, object> objects)
        {
            return objects.ToDictionary(
                pair => pair.Key,
                pair => pair.Value?.ToString() ?? string.Empty,
                StringComparer.Ordinal);
        }

        if (notes is IDictionary<string, string> strings)
        {
            return new Dictionary<string, string>(strings, StringComparer.Ordinal);
        }

        return new Dictionary<string, string>();
    }
}
