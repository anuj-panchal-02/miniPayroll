using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Entities;

public class Subscription
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid PlanId { get; set; }
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Pending;
    public int EmployeeLimit { get; set; }
    public int GracePeriodDays { get; set; }
    public DateTimeOffset? CurrentPeriodStart { get; set; }
    public DateTimeOffset? CurrentPeriodEnd { get; set; }
    public DateTimeOffset? DueDate { get; set; }

    public Company Company { get; set; } = null!;
    public Plan Plan { get; set; } = null!;
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public ICollection<BillingPeriodSnapshot> BillingPeriods { get; set; } = new List<BillingPeriodSnapshot>();
}
