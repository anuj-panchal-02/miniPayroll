using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Entities;

public class SubscriptionEvent
{
    public Guid Id { get; set; }
    public Guid SubscriptionId { get; set; }
    public Guid CompanyId { get; set; }
    public SubscriptionEventType Type { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public Guid? ActorUserId { get; set; }

    public Subscription Subscription { get; set; } = null!;
}
