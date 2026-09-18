namespace MiniPayroll.Domain.Billing.Payments;

public enum PaymentProviderStatus
{
    Succeeded,
    Failed,
    Timeout,
    Unavailable,
    Duplicate
}

public enum PaymentLifecycleStatus
{
    Succeeded,
    Failed,
    Pending,
    Unknown
}

public enum PaymentWebhookEventType
{
    PaymentSucceeded,
    PaymentFailed,
    RecurringCancelled,
    Ignored,
    SubscriptionActivated,
    SubscriptionPaused,
    SubscriptionResumed,
    Refunded
}
