using MiniPayroll.Api.Auth;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Infrastructure.Payments;

namespace MiniPayroll.Api.Endpoints;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentEndpoints(this IEndpointRouteBuilder routes)
    {
        var payments = routes.MapGroup("/api/payments")
            .RequireAuthorization(policy => policy
                .RequireRole(RoleNames.CompanyAdmin, RoleNames.Superadmin)
                .AddRequirements(new PasswordChangeCompletedRequirement()));
        payments.MapPost("/checkout", Checkout);
        payments.MapPost("/verify", Verify);
        payments.MapPost("/recurring", Recurring);
        payments.MapPost("/recurring/cancel", CancelRecurring);

        routes.MapPost("/api/webhooks/razorpay", RazorpayWebhook).AllowAnonymous();
        return routes;
    }

    private static async Task<IResult> Checkout(
        CheckoutRequest request,
        PaymentReconciliationService payments,
        CancellationToken cancellationToken) =>
        ToHttp(await payments.StartCheckoutAsync(
            request.CompanyId,
            request.Amount,
            request.Currency,
            request.IdempotencyKey,
            request.InvoiceId,
            cancellationToken));

    private static async Task<IResult> Verify(
        VerifyRequest request,
        PaymentReconciliationService payments,
        CancellationToken cancellationToken)
    {
        var result = string.IsNullOrWhiteSpace(request.ProviderSubscriptionId)
            ? await payments.VerifyAsync(
                request.CompanyId,
                request.ProviderOrderId,
                request.ProviderPaymentId,
                request.Signature,
                cancellationToken)
            : await payments.VerifyRecurringAsync(
                request.CompanyId,
                request.ProviderSubscriptionId,
                request.ProviderPaymentId,
                request.Signature,
                cancellationToken);
        return ToHttp(result);
    }

    private static async Task<IResult> Recurring(
        RecurringRequest request,
        PaymentReconciliationService payments,
        CancellationToken cancellationToken) =>
        ToHttp(await payments.StartRecurringAsync(
            request.CompanyId,
            request.PlanCode,
            request.Currency,
            request.Amount,
            request.IdempotencyKey,
            request.PeriodStart,
            request.PeriodEnd,
            cancellationToken));

    private static async Task<IResult> CancelRecurring(
        CancelRecurringRequest request,
        PaymentReconciliationService payments,
        CancellationToken cancellationToken) =>
        ToHttp(await payments.CancelRecurringAsync(
            request.CompanyId,
            request.ProviderSubscriptionId,
            request.IdempotencyKey,
            cancellationToken));

    private static async Task<IResult> RazorpayWebhook(
        HttpRequest request,
        PaymentReconciliationService payments,
        CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(request.Body);
        var body = await reader.ReadToEndAsync(cancellationToken);
        var headers = request.Headers.ToDictionary(
            header => header.Key,
            header => header.Value.ToString(),
            StringComparer.OrdinalIgnoreCase);
        headers.TryGetValue("X-Razorpay-Signature", out var signature);
        return ToHttp(await payments.HandleWebhookAsync(body, signature, headers, cancellationToken));
    }

    private static IResult ToHttp(PaymentReconciliationResult result) =>
        result.Status is PaymentReconciliationStatus.Success
            or PaymentReconciliationStatus.Duplicate
            or PaymentReconciliationStatus.Ignored
            ? Results.Ok(new
            {
                status = result.Status.ToString(),
                checkoutUrl = result.CheckoutUrl,
                clientKey = result.ClientKey,
                providerOrderId = result.ProviderOrderId,
                providerPaymentId = result.ProviderPaymentId,
                providerSubscriptionId = result.ProviderSubscriptionId
            })
            : Results.Json(new { error = result.Error ?? "The payment request could not be completed." },
                statusCode: PaymentHttpStatus.For(result.Status));
}

public static class PaymentHttpStatus
{
    public static int For(PaymentReconciliationStatus status) =>
        status switch
        {
            PaymentReconciliationStatus.Success
                or PaymentReconciliationStatus.Duplicate
                or PaymentReconciliationStatus.Ignored => StatusCodes.Status200OK,
            PaymentReconciliationStatus.Retryable => StatusCodes.Status500InternalServerError,
            PaymentReconciliationStatus.Forbidden => StatusCodes.Status403Forbidden,
            PaymentReconciliationStatus.CompanyNotFound or PaymentReconciliationStatus.NotFound => StatusCodes.Status404NotFound,
            PaymentReconciliationStatus.Timeout => StatusCodes.Status504GatewayTimeout,
            PaymentReconciliationStatus.Unavailable => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status400BadRequest
        };
}

public sealed record CheckoutRequest(
    Guid? CompanyId,
    decimal Amount,
    string Currency,
    string IdempotencyKey,
    Guid? InvoiceId = null);

public sealed record VerifyRequest(
    Guid? CompanyId,
    string ProviderPaymentId,
    string? ProviderOrderId = null,
    string? Signature = null,
    string? ProviderSubscriptionId = null);

public sealed record RecurringRequest(
    Guid? CompanyId,
    string PlanCode,
    string Currency,
    decimal Amount,
    string IdempotencyKey,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd);

public sealed record CancelRecurringRequest(
    Guid? CompanyId,
    string ProviderSubscriptionId,
    string IdempotencyKey);
