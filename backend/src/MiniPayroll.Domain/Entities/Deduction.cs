using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Entities;

public class Deduction
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid PayrollRunId { get; set; }
    public Guid EmployeeId { get; set; }

    public OneTimeDeductionType Type { get; set; }
    public decimal Amount { get; set; }
    public string? Notes { get; set; }

    public PayrollRun PayrollRun { get; set; } = null!;
    public Employee Employee { get; set; } = null!;
}
