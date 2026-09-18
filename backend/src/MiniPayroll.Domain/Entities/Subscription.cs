using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Entities;

public class Subscription
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid PlanId { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trialing;
    public BillingCycle BillingCycle { get; set; } = BillingCycle.Monthly;
    public int EmployeeLimit { get; set; }
    public int GracePeriodDays { get; set; }
    public DateTimeOffset? CurrentPeriodStart { get; set; }
    public DateTimeOffset? CurrentPeriodEnd { get; set; }
    public DateTimeOffset? NextBillingDate { get; set; }
    public DateTimeOffset? DueDate { get; set; }
    public DateTimeOffset? TrialStartedAt { get; set; }
    public DateTimeOffset? TrialEndsAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public bool CancelAtPeriodEnd { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Company Company { get; set; } = null!;
    public Plan Plan { get; set; } = null!;
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<BillingPeriodSnapshot> BillingPeriods { get; set; } = new List<BillingPeriodSnapshot>();
    public ICollection<SubscriptionEvent> Events { get; set; } = new List<SubscriptionEvent>();
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
