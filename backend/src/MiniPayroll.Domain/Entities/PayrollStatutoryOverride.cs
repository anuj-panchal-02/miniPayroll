using MiniPayroll.Domain.Payroll.Statutory;

namespace MiniPayroll.Domain.Entities;

public class PayrollStatutoryOverride
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid PayrollRunId { get; set; }
    public Guid EmployeeId { get; set; }
    public StatutoryKind Kind { get; set; }
    public decimal Amount { get; set; }

    public PayrollRun PayrollRun { get; set; } = null!;
    public Employee Employee { get; set; } = null!;
}
