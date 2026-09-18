namespace MiniPayroll.Domain.Enums;

public enum SubscriptionEventType
{
    TrialStarted = 0,
    Activated = 1,
    PlanChanged = 2,
    PaymentSucceeded = 3,
    PaymentFailed = 4,
    CancellationRequested = 5,
    Cancelled = 6,
    Suspended = 7,
    Reactivated = 8,
    Expired = 9
}
