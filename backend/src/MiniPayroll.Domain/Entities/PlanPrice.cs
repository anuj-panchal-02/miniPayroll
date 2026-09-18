using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Entities;

public class PlanPrice
{
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public BillingCycle BillingCycle { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;

    public Plan Plan { get; set; } = null!;
}
