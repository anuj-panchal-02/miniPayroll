namespace MiniPayroll.Domain.Entities;

public class Plan
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public int MaxActiveEmployees { get; set; }
    public int TrialDays { get; set; }
    public decimal PricePerEmployee { get; set; }
    public int DefaultEmployeeLimit { get; set; }
    public bool IsPublic { get; set; } = true;

    public ICollection<PlanPrice> Prices { get; set; } = new List<PlanPrice>();
    public ICollection<PlanFeature> Features { get; set; } = new List<PlanFeature>();
    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
