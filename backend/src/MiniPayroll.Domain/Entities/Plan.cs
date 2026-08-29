namespace MiniPayroll.Domain.Entities;

public class Plan
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal PricePerEmployee { get; set; }
    public int DefaultEmployeeLimit { get; set; }
    public bool IsPublic { get; set; } = true;

    public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
}
