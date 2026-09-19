namespace MiniPayroll.Infrastructure.Payments.Razorpay;

public sealed record RazorpayOrderCreateRequest(
    int AmountPaise,
    string Currency,
    string Receipt,
    IReadOnlyDictionary<string, string> Notes);

public sealed record RazorpayOrderRecord(
    string Id,
    int AmountPaise,
    string Currency,
    IReadOnlyDictionary<string, string> Notes);

public sealed record RazorpayPaymentLinkCreateRequest(
    int AmountPaise,
    string Currency,
    string Description,
    string ReferenceId,
    IReadOnlyDictionary<string, string> Notes);

public sealed record RazorpayPaymentLinkRecord(
    string Id,
    string ShortUrl,
    int AmountPaise,
    string Currency,
    IReadOnlyDictionary<string, string> Notes);

public sealed record RazorpayPaymentRecord(
    string Id,
    string? OrderId,
    string Status,
    int AmountPaise,
    string Currency,
    IReadOnlyDictionary<string, string> Notes,
    string? SubscriptionId = null);

public sealed record RazorpaySubscriptionCreateRequest(
    string PlanId,
    int TotalCount,
    IReadOnlyDictionary<string, string> Notes,
    int Quantity = 1);

public sealed record RazorpaySubscriptionRecord(
    string Id,
    string Status,
    IReadOnlyDictionary<string, string> Notes);

public sealed class RazorpayClientException : Exception
{
    public RazorpayClientException(string status, string message, Exception? inner = null)
        : base(message, inner)
    {
        Status = status;
    }

    public string Status { get; }
}
