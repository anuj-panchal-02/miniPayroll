using MiniPayroll.Domain.Billing;

namespace MiniPayroll.Domain.Entities;

public class BillingPeriodSnapshot
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid SubscriptionId { get; set; }
    public string BillingPeriod { get; set; } = string.Empty;
    public decimal PricePerEmployee { get; set; }
    public int BillableEmployees { get; set; }
    public BillableSource BillableSource { get; set; }
    public decimal AmountDue { get; set; }
    public bool Prorated { get; set; }
    public DateTimeOffset DueDate { get; set; }

    public Company Company { get; set; } = null!;
    public Subscription Subscription { get; set; } = null!;
}
