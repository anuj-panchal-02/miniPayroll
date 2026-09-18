namespace MiniPayroll.Domain.Entities;

public class PlanFeature
{
    public Guid Id { get; set; }
    public Guid PlanId { get; set; }
    public string Code { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public int? Limit { get; set; }

    public Plan Plan { get; set; } = null!;
}
