using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Billing.Payments;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Subscriptions;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Infrastructure.Payments;

public enum PaymentReconciliationStatus
{
    Success,
    Duplicate,
    Forbidden,
    CompanyNotFound,
    NotFound,
    Failed,
    Timeout,
    Unavailable,
    InvalidInput,
    Ignored,
    Retryable
}

public sealed record PaymentReconciliationResult(
    PaymentReconciliationStatus Status,
    string? CheckoutUrl = null,
    string? ClientKey = null,
    string? ProviderOrderId = null,
    string? ProviderPaymentId = null,
    string? ProviderSubscriptionId = null,
    string? Error = null);

public sealed class PaymentReconciliationService(
    MiniPayrollDbContext db,
    ITenantContext tenant,
    PaymentGatewayService gateway,
    InvoiceService invoices,
    SubscriptionLifecycleService subscriptions,
    TimeProvider? time = null)
{
    public async Task<PaymentReconciliationResult> StartCheckoutAsync(
        Guid? requestedCompanyId,
        decimal amount,
        string currency,
        string idempotencyKey,
        Guid? invoiceId,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveCompany(requestedCompanyId, webhook: false, out var companyId, out var denied))
        {
            return denied;
        }

        if (amount <= 0 || string.IsNullOrWhiteSpace(currency) || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return new PaymentReconciliationResult(PaymentReconciliationStatus.InvalidInput, Error: "Checkout input was invalid.");
        }

        var existing = await IntentByKeyAsync(idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            if (existing.CompanyId != companyId)
            {
                return new PaymentReconciliationResult(PaymentReconciliationStatus.Forbidden, Error: "You are not allowed to use this checkout.");
            }

            return Replay(existing);
        }

        var company = await LoadCompanyAsync(companyId, cancellationToken);
        if (company?.Subscription is null)
        {
            return new PaymentReconciliationResult(PaymentReconciliationStatus.CompanyNotFound, Error: "The company was not found.");
        }

        var now = Now();
        var intent = new PaymentIntent
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            SubscriptionId = company.Subscription.Id,
            InvoiceId = invoiceId,
            Amount = amount,
            Currency = currency.Trim().ToUpperInvariant(),
            IdempotencyKey = idempotencyKey,
            Status = PaymentIntentStatus.Created,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.PaymentIntents.Add(intent);
        await db.SaveChangesAsync(cancellationToken);

        var checkout = await gateway.CreateCheckoutAsync(
            new PaymentCheckoutRequest(company.Id, amount, intent.Currency, idempotencyKey, invoiceId),
            cancellationToken);
        if (checkout.Status is not PaymentProviderStatus.Succeeded and not PaymentProviderStatus.Duplicate)
        {
            return FromProvider(checkout.Status, checkout.Error);
        }

        intent.ProviderOrderId = checkout.ProviderOrderId;
        intent.ProviderPaymentId = checkout.ProviderPaymentId;
        intent.UpdatedAt = Now();
        await db.SaveChangesAsync(cancellationToken);
        return new PaymentReconciliationResult(
            checkout.Status == PaymentProviderStatus.Duplicate
                ? PaymentReconciliationStatus.Duplicate
                : PaymentReconciliationStatus.Success,
            checkout.CheckoutUrl,
            checkout.ClientKey,
            checkout.ProviderOrderId,
            checkout.ProviderPaymentId);
    }

    public async Task<PaymentReconciliationResult> VerifyAsync(
        Guid? requestedCompanyId,
        string? providerOrderId,
        string providerPaymentId,
        string? signature,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveCompany(requestedCompanyId, webhook: false, out var companyId, out var denied))
        {
            return denied;
        }

        var verified = await gateway.VerifyPaymentAsync(
            new PaymentVerificationRequest(providerPaymentId, signature, ProviderOrderId: providerOrderId),
            cancellationToken);
        return await ApplyVerifiedAsync(companyId, verified, persist: true, cancellationToken);
    }

    public async Task<PaymentReconciliationResult> StartRecurringAsync(
        Guid? requestedCompanyId,
        string planCode,
        string currency,
        decimal amount,
        string idempotencyKey,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveCompany(requestedCompanyId, webhook: false, out var companyId, out var denied))
        {
            return denied;
        }

        if (string.IsNullOrWhiteSpace(planCode) || string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return new PaymentReconciliationResult(PaymentReconciliationStatus.InvalidInput, Error: "Recurring input was invalid.");
        }

        var existing = await IntentByKeyAsync(idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return existing.CompanyId != companyId
                ? new PaymentReconciliationResult(PaymentReconciliationStatus.Forbidden)
                : Replay(existing);
        }

        var company = await LoadCompanyAsync(companyId, cancellationToken);
        if (company?.Subscription is null)
        {
            return new PaymentReconciliationResult(PaymentReconciliationStatus.CompanyNotFound, Error: "The company was not found.");
        }

        var now = Now();
        var intent = new PaymentIntent
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            SubscriptionId = company.Subscription.Id,
            Amount = amount,
            Currency = currency.Trim().ToUpperInvariant(),
            IdempotencyKey = idempotencyKey,
            Status = PaymentIntentStatus.Created,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.PaymentIntents.Add(intent);
        await db.SaveChangesAsync(cancellationToken);

        var created = await gateway.CreateRecurringAsync(
            new PaymentRecurringRequest(company.Id, planCode, intent.Currency, amount, idempotencyKey, periodStart, periodEnd),
            cancellationToken);
        if (created.Status is not PaymentProviderStatus.Succeeded and not PaymentProviderStatus.Duplicate)
        {
            return FromProvider(created.Status, created.Error);
        }

        intent.ProviderSubscriptionId = created.ProviderSubscriptionId;
        intent.UpdatedAt = Now();
        await db.SaveChangesAsync(cancellationToken);
        return new PaymentReconciliationResult(
            created.Status == PaymentProviderStatus.Duplicate
                ? PaymentReconciliationStatus.Duplicate
                : PaymentReconciliationStatus.Success,
            ProviderSubscriptionId: created.ProviderSubscriptionId);
    }

    public async Task<PaymentReconciliationResult> CancelRecurringAsync(
        Guid? requestedCompanyId,
        string providerSubscriptionId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveCompany(requestedCompanyId, webhook: false, out var companyId, out var denied))
        {
            return denied;
        }

        var intent = await Intents()
            .FirstOrDefaultAsync(
                item => item.CompanyId == companyId && item.ProviderSubscriptionId == providerSubscriptionId,
                cancellationToken);
        if (intent is null)
        {
            return new PaymentReconciliationResult(PaymentReconciliationStatus.NotFound, Error: "The subscription was not found.");
        }

        var cancelled = await gateway.CancelRecurringAsync(
            new PaymentCancelRecurringRequest(providerSubscriptionId, idempotencyKey),
            cancellationToken);
        return cancelled.Status is PaymentProviderStatus.Succeeded or PaymentProviderStatus.Duplicate
            ? new PaymentReconciliationResult(
                cancelled.Status == PaymentProviderStatus.Duplicate
                    ? PaymentReconciliationStatus.Duplicate
                    : PaymentReconciliationStatus.Success,
                ProviderSubscriptionId: cancelled.ProviderSubscriptionId)
            : FromProvider(cancelled.Status, cancelled.Error);
    }

    public async Task<PaymentReconciliationResult> VerifyRecurringAsync(
        Guid? requestedCompanyId,
        string providerSubscriptionId,
        string providerPaymentId,
        string? signature,
        CancellationToken cancellationToken = default)
    {
        if (!TryResolveCompany(requestedCompanyId, webhook: false, out var companyId, out var denied))
        {
            return denied;
        }

        var verified = await gateway.VerifyRecurringAsync(
            new PaymentRecurringVerificationRequest(providerSubscriptionId, providerPaymentId, signature),
            cancellationToken);
        return await ApplyVerifiedAsync(companyId, verified, persist: true, cancellationToken);
    }

    public async Task<PaymentReconciliationResult> HandleWebhookAsync(
        string rawBody,
        string? signature,
        IReadOnlyDictionary<string, string> headers,
        CancellationToken cancellationToken = default)
    {
        var webhook = await gateway.HandleWebhookAsync(
            new PaymentWebhookRequest(rawBody, signature, headers),
            cancellationToken);
        if (webhook.Status == PaymentProviderStatus.Failed
            && string.Equals(webhook.Error, "Webhook signature was not valid.", StringComparison.Ordinal))
        {
            return new PaymentReconciliationResult(PaymentReconciliationStatus.Failed, Error: webhook.Error);
        }

        if (webhook.Status is PaymentProviderStatus.Timeout or PaymentProviderStatus.Unavailable)
        {
            return FromProvider(webhook.Status, webhook.Error);
        }

        if (webhook.Status != PaymentProviderStatus.Succeeded)
        {
            return FromProvider(webhook.Status, webhook.Error);
        }

        var eventId = webhook.ProviderEventId ?? $"{webhook.Type}:{webhook.ProviderPaymentId}";
        var stored = await db.PaymentProviderEvents
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.ExternalEventId == eventId, cancellationToken);
        if (stored is { ProcessingStatus: PaymentWebhookProcessingStatus.Processed or PaymentWebhookProcessingStatus.Ignored })
        {
            return stored.IntentId is { } intentId
                && await Intents().FirstOrDefaultAsync(item => item.Id == intentId, cancellationToken) is { } previous
                ? Replay(previous)
                : new PaymentReconciliationResult(PaymentReconciliationStatus.Duplicate);
        }

        var intent = await FindIntentAsync(
            webhook.ProviderOrderId,
            webhook.ProviderPaymentId,
            webhook.ProviderSubscriptionId,
            cancellationToken);
        if (stored is null)
        {
            stored = NewWebhookEvent(eventId, webhook, rawBody, intent);
            db.PaymentProviderEvents.Add(stored);
        }

        if (webhook.Type == PaymentWebhookEventType.Ignored)
        {
            CompleteEvent(stored, PaymentWebhookProcessingStatus.Ignored, intent);
            await db.SaveChangesAsync(cancellationToken);
            return new PaymentReconciliationResult(
                PaymentReconciliationStatus.Ignored,
                ProviderOrderId: webhook.ProviderOrderId,
                ProviderPaymentId: webhook.ProviderPaymentId,
                ProviderSubscriptionId: webhook.ProviderSubscriptionId);
        }

        try
        {
            var applied = await ApplyWebhookAsync(webhook, intent, cancellationToken);
            if (applied.Status == PaymentReconciliationStatus.Retryable)
            {
                FailEvent(stored, applied.Error ?? "Webhook processing failed.", intent);
                await db.SaveChangesAsync(cancellationToken);
                return applied;
            }

            CompleteEvent(stored, PaymentWebhookProcessingStatus.Processed, intent);
            await db.SaveChangesAsync(cancellationToken);
            return applied;
        }
        catch (Exception exception)
        {
            FailEvent(stored, exception.Message, intent);
            await db.SaveChangesAsync(cancellationToken);
            return new PaymentReconciliationResult(PaymentReconciliationStatus.Retryable, Error: exception.Message);
        }
    }

    private async Task<PaymentReconciliationResult> ApplyWebhookAsync(
        PaymentWebhookEvent webhook,
        PaymentIntent? intent,
        CancellationToken cancellationToken)
    {
        return webhook.Type switch
        {
            PaymentWebhookEventType.PaymentSucceeded => await ApplyVerifiedAsync(
                null,
                new PaymentVerificationResult(
                    PaymentProviderStatus.Succeeded,
                    webhook.ProviderPaymentId,
                    webhook.Amount,
                    Currency: webhook.Currency,
                    ProviderOrderId: webhook.ProviderOrderId,
                    ProviderSubscriptionId: webhook.ProviderSubscriptionId,
                    CompanyId: webhook.CompanyId),
                persist: false,
                cancellationToken),
            PaymentWebhookEventType.PaymentFailed => await ApplyPaymentFailedAsync(intent, webhook, cancellationToken),
            PaymentWebhookEventType.SubscriptionActivated =>
                await ApplySubscriptionAsync(intent, webhook, SubscriptionCommand.Activate, cancellationToken),
            PaymentWebhookEventType.SubscriptionResumed =>
                await ApplySubscriptionAsync(intent, webhook, SubscriptionCommand.Reactivate, cancellationToken),
            PaymentWebhookEventType.SubscriptionPaused =>
                await ApplySubscriptionAsync(intent, webhook, SubscriptionCommand.Suspend, cancellationToken),
            PaymentWebhookEventType.RecurringCancelled =>
                await ApplySubscriptionAsync(intent, webhook, SubscriptionCommand.CancelNow, cancellationToken),
            PaymentWebhookEventType.Refunded => await ApplyRefundAsync(intent, webhook, cancellationToken),
            _ => new PaymentReconciliationResult(PaymentReconciliationStatus.Ignored)
        };
    }

    private async Task<PaymentReconciliationResult> ApplyPaymentFailedAsync(
        PaymentIntent? intent,
        PaymentWebhookEvent webhook,
        CancellationToken cancellationToken)
    {
        if (intent is null)
        {
            return new PaymentReconciliationResult(
                PaymentReconciliationStatus.Success,
                ProviderOrderId: webhook.ProviderOrderId,
                ProviderPaymentId: webhook.ProviderPaymentId);
        }

        if (intent.Status == PaymentIntentStatus.Verified)
        {
            return new PaymentReconciliationResult(
                PaymentReconciliationStatus.Success,
                ProviderOrderId: intent.ProviderOrderId,
                ProviderPaymentId: intent.ProviderPaymentId,
                ProviderSubscriptionId: intent.ProviderSubscriptionId);
        }

        intent.Status = PaymentIntentStatus.Failed;
        intent.ProviderPaymentId ??= webhook.ProviderPaymentId;
        intent.UpdatedAt = Now();
        if (intent.InvoiceId is { } invoiceId)
        {
            await invoices.MarkTrustedFailedAsync(intent.CompanyId, invoiceId, persist: false, cancellationToken);
        }

        return new PaymentReconciliationResult(
            PaymentReconciliationStatus.Success,
            ProviderOrderId: intent.ProviderOrderId,
            ProviderPaymentId: intent.ProviderPaymentId,
            ProviderSubscriptionId: intent.ProviderSubscriptionId);
    }

    private async Task<PaymentReconciliationResult> ApplySubscriptionAsync(
        PaymentIntent? intent,
        PaymentWebhookEvent webhook,
        SubscriptionCommand command,
        CancellationToken cancellationToken)
    {
        var companyId = intent?.CompanyId ?? webhook.CompanyId;
        if (companyId is null)
        {
            return new PaymentReconciliationResult(PaymentReconciliationStatus.Success);
        }

        var applied = await subscriptions.ApplyTrustedCommandAsync(
            companyId.Value,
            command,
            ActorId(),
            persist: false,
            cancellationToken);
        return applied.Status is SubscriptionCommandStatus.Success or SubscriptionCommandStatus.InvalidTransition
            ? new PaymentReconciliationResult(
                PaymentReconciliationStatus.Success,
                ProviderOrderId: webhook.ProviderOrderId,
                ProviderPaymentId: webhook.ProviderPaymentId,
                ProviderSubscriptionId: webhook.ProviderSubscriptionId ?? intent?.ProviderSubscriptionId)
            : new PaymentReconciliationResult(PaymentReconciliationStatus.Retryable, Error: "Subscription could not be updated.");
    }

    private async Task<PaymentReconciliationResult> ApplyRefundAsync(
        PaymentIntent? intent,
        PaymentWebhookEvent webhook,
        CancellationToken cancellationToken)
    {
        if (intent?.InvoiceId is not { } invoiceId)
        {
            return new PaymentReconciliationResult(PaymentReconciliationStatus.Success);
        }

        await invoices.MarkTrustedRefundedAsync(intent.CompanyId, invoiceId, persist: false, cancellationToken);
        return new PaymentReconciliationResult(
            PaymentReconciliationStatus.Success,
            ProviderOrderId: intent.ProviderOrderId,
            ProviderPaymentId: intent.ProviderPaymentId,
            ProviderSubscriptionId: intent.ProviderSubscriptionId);
    }

    private async Task<PaymentReconciliationResult> ApplyVerifiedAsync(
        Guid? expectedCompanyId,
        PaymentVerificationResult verified,
        bool persist,
        CancellationToken cancellationToken)
    {
        if (verified.Status != PaymentProviderStatus.Succeeded)
        {
            var failed = await FindIntentAsync(
                verified.ProviderOrderId,
                verified.ProviderPaymentId,
                verified.ProviderSubscriptionId,
                cancellationToken);
            if (failed is not null && failed.Status != PaymentIntentStatus.Verified)
            {
                failed.Status = PaymentIntentStatus.Failed;
                failed.ProviderPaymentId ??= verified.ProviderPaymentId;
                failed.UpdatedAt = Now();
                if (persist)
                {
                    await db.SaveChangesAsync(cancellationToken);
                }
            }

            return FromProvider(verified.Status, verified.Error);
        }

        var intent = await FindIntentAsync(
            verified.ProviderOrderId,
            verified.ProviderPaymentId,
            verified.ProviderSubscriptionId,
            cancellationToken);
        if (intent is null)
        {
            return persist
                ? new PaymentReconciliationResult(PaymentReconciliationStatus.NotFound, Error: "The payment was not found.")
                : new PaymentReconciliationResult(PaymentReconciliationStatus.Retryable, Error: "The payment was not found.");
        }

        if (expectedCompanyId is { } companyId && intent.CompanyId != companyId)
        {
            return new PaymentReconciliationResult(PaymentReconciliationStatus.Forbidden, Error: "You are not allowed to verify this payment.");
        }

        if (verified.CompanyId is { } noted && noted != intent.CompanyId)
        {
            return persist
                ? await FailAsync(intent, "Payment company did not match.", cancellationToken)
                : new PaymentReconciliationResult(PaymentReconciliationStatus.Failed, Error: "Payment company did not match.");
        }

        if (verified.Amount is { } amount && Razorpay.RazorpayMoney.ToPaise(intent.Amount) != Razorpay.RazorpayMoney.ToPaise(amount))
        {
            return persist
                ? await FailAsync(intent, "Payment amount did not match.", cancellationToken)
                : new PaymentReconciliationResult(PaymentReconciliationStatus.Failed, Error: "Payment amount did not match.");
        }

        if (verified.Currency is { } currency
            && !string.Equals(currency, intent.Currency, StringComparison.OrdinalIgnoreCase))
        {
            return persist
                ? await FailAsync(intent, "Payment currency did not match.", cancellationToken)
                : new PaymentReconciliationResult(PaymentReconciliationStatus.Failed, Error: "Payment currency did not match.");
        }

        if (intent.Status == PaymentIntentStatus.Verified)
        {
            return new PaymentReconciliationResult(
                PaymentReconciliationStatus.Success,
                ProviderOrderId: intent.ProviderOrderId,
                ProviderPaymentId: intent.ProviderPaymentId,
                ProviderSubscriptionId: intent.ProviderSubscriptionId);
        }

        intent.ProviderPaymentId ??= verified.ProviderPaymentId;
        intent.ProviderOrderId ??= verified.ProviderOrderId;
        intent.ProviderSubscriptionId ??= verified.ProviderSubscriptionId;

        if (intent.InvoiceId is { } invoiceId)
        {
            var applied = await invoices.ApplyTrustedPaymentAsync(
                intent.CompanyId,
                invoiceId,
                intent.Amount,
                persist: false,
                cancellationToken);
            if (applied.Status is not InvoiceCommandStatus.Success)
            {
                return persist
                    ? await FailAsync(intent, "Invoice payment could not be applied.", cancellationToken)
                    : new PaymentReconciliationResult(
                        PaymentReconciliationStatus.Retryable,
                        Error: "Invoice payment could not be applied.");
            }
        }

        await subscriptions.ActivateFromTrustedPaymentAsync(
            intent.CompanyId,
            ActorId(),
            persist: false,
            cancellationToken);
        intent.Status = PaymentIntentStatus.Verified;
        intent.UpdatedAt = Now();
        if (persist)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return new PaymentReconciliationResult(
            PaymentReconciliationStatus.Success,
            ProviderOrderId: intent.ProviderOrderId,
            ProviderPaymentId: intent.ProviderPaymentId,
            ProviderSubscriptionId: intent.ProviderSubscriptionId);
    }

    private PaymentProviderEvent NewWebhookEvent(
        string eventId,
        PaymentWebhookEvent webhook,
        string rawBody,
        PaymentIntent? intent)
    {
        var now = Now();
        return new PaymentProviderEvent
        {
            Id = Guid.NewGuid(),
            Provider = PaymentProviders.Razorpay,
            ExternalEventId = eventId,
            EventType = webhook.Type,
            ProcessingStatus = PaymentWebhookProcessingStatus.Received,
            PayloadHash = HashPayload(rawBody),
            CompanyId = intent?.CompanyId ?? webhook.CompanyId,
            IntentId = intent?.Id,
            SubscriptionId = intent?.SubscriptionId,
            InvoiceId = intent?.InvoiceId,
            ReceivedAt = now
        };
    }

    private void CompleteEvent(
        PaymentProviderEvent stored,
        PaymentWebhookProcessingStatus status,
        PaymentIntent? intent)
    {
        stored.ProcessingStatus = status;
        stored.ProcessedAt = Now();
        stored.Error = null;
        stored.IntentId ??= intent?.Id;
        stored.CompanyId ??= intent?.CompanyId;
        stored.SubscriptionId ??= intent?.SubscriptionId;
        stored.InvoiceId ??= intent?.InvoiceId;
    }

    private void FailEvent(PaymentProviderEvent stored, string error, PaymentIntent? intent)
    {
        stored.ProcessingStatus = PaymentWebhookProcessingStatus.Failed;
        stored.Error = error;
        stored.ProcessedAt = null;
        stored.IntentId ??= intent?.Id;
        stored.CompanyId ??= intent?.CompanyId;
        stored.SubscriptionId ??= intent?.SubscriptionId;
        stored.InvoiceId ??= intent?.InvoiceId;
    }

    private static string HashPayload(string rawBody) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawBody ?? string.Empty)));

    private async Task<PaymentReconciliationResult> FailAsync(
        PaymentIntent intent,
        string error,
        CancellationToken cancellationToken)
    {
        if (intent.Status != PaymentIntentStatus.Verified)
        {
            intent.Status = PaymentIntentStatus.Failed;
            intent.UpdatedAt = Now();
            await db.SaveChangesAsync(cancellationToken);
        }

        return new PaymentReconciliationResult(PaymentReconciliationStatus.Failed, Error: error);
    }

    private bool TryResolveCompany(
        Guid? requestedCompanyId,
        bool webhook,
        out Guid companyId,
        out PaymentReconciliationResult denied)
    {
        companyId = Guid.Empty;
        denied = new PaymentReconciliationResult(PaymentReconciliationStatus.Forbidden);
        if (webhook)
        {
            return true;
        }

        if (tenant.IsSuperadmin)
        {
            if (requestedCompanyId is not { } requested || requested == Guid.Empty)
            {
                denied = new PaymentReconciliationResult(
                    PaymentReconciliationStatus.InvalidInput,
                    Error: "companyId is required.");
                return false;
            }

            companyId = requested;
            return true;
        }

        if (tenant.CompanyId is not { } own)
        {
            denied = new PaymentReconciliationResult(PaymentReconciliationStatus.Forbidden, Error: "You are not allowed to take this payment.");
            return false;
        }

        if (requestedCompanyId is { } other && other != own)
        {
            denied = new PaymentReconciliationResult(PaymentReconciliationStatus.Forbidden, Error: "You are not allowed to take this payment.");
            return false;
        }

        companyId = own;
        return true;
    }

    private async Task<Company?> LoadCompanyAsync(Guid companyId, CancellationToken cancellationToken) =>
        await db.Companies
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(company => company.Subscription)
            .FirstOrDefaultAsync(company => company.Id == companyId, cancellationToken);

    private IQueryable<PaymentIntent> Intents() =>
        db.PaymentIntents.IgnoreQueryFilters();

    private Task<PaymentIntent?> IntentByKeyAsync(string key, CancellationToken cancellationToken) =>
        Intents().FirstOrDefaultAsync(item => item.IdempotencyKey == key, cancellationToken);

    private async Task<PaymentIntent?> FindIntentAsync(
        string? orderId,
        string? paymentId,
        string? subscriptionId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(orderId))
        {
            var byOrder = await Intents().FirstOrDefaultAsync(item => item.ProviderOrderId == orderId, cancellationToken);
            if (byOrder is not null)
            {
                return byOrder;
            }
        }

        if (!string.IsNullOrWhiteSpace(paymentId))
        {
            var byPayment = await Intents().FirstOrDefaultAsync(item => item.ProviderPaymentId == paymentId, cancellationToken);
            if (byPayment is not null)
            {
                return byPayment;
            }
        }

        if (!string.IsNullOrWhiteSpace(subscriptionId))
        {
            return await Intents().FirstOrDefaultAsync(
                item => item.ProviderSubscriptionId == subscriptionId,
                cancellationToken);
        }

        return null;
    }

    private static PaymentReconciliationResult Replay(PaymentIntent intent) =>
        new(
            PaymentReconciliationStatus.Duplicate,
            ProviderOrderId: intent.ProviderOrderId,
            ProviderPaymentId: intent.ProviderPaymentId,
            ProviderSubscriptionId: intent.ProviderSubscriptionId);

    private static PaymentReconciliationResult FromProvider(PaymentProviderStatus status, string? error) =>
        new(
            status switch
            {
                PaymentProviderStatus.Failed => PaymentReconciliationStatus.Failed,
                PaymentProviderStatus.Timeout => PaymentReconciliationStatus.Timeout,
                PaymentProviderStatus.Unavailable => PaymentReconciliationStatus.Unavailable,
                PaymentProviderStatus.Duplicate => PaymentReconciliationStatus.Duplicate,
                _ => PaymentReconciliationStatus.Failed
            },
            Error: error);

    private DateTimeOffset Now() => (time ?? TimeProvider.System).GetUtcNow();

    private Guid? ActorId() => tenant.UserId == Guid.Empty ? null : tenant.UserId;
}
