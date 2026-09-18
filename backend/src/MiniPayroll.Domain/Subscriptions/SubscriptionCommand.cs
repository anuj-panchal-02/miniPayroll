namespace MiniPayroll.Domain.Subscriptions;

public enum SubscriptionCommand
{
    Activate,
    MarkPastDue,
    EnterGrace,
    Suspend,
    RequestCancel,
    CancelNow,
    Expire,
    Reactivate
}
