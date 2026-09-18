namespace MiniPayroll.Domain.Enums;

public enum SubscriptionStatus
{
    Trialing = 0,
    Active = 1,
    PastDue = 2,
    Suspended = 3,
    Cancelled = 4,
    GracePeriod = 5,
    Expired = 6
}
