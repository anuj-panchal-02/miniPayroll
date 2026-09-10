using MiniPayroll.Domain.Payroll;

namespace MiniPayroll.Domain.Entities;

/// <summary>Snapshot earning line for a payroll employee result.</summary>
public class PayrollEarning
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid PayrollEmployeeId { get; set; }

    public string Name { get; set; } = string.Empty;
    public PayrollLineKind Kind { get; set; }
    public decimal Amount { get; set; }
    public int SortOrder { get; set; }

    public PayrollEmployee PayrollEmployee { get; set; } = null!;
}
