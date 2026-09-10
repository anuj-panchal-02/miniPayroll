namespace MiniPayroll.Domain.Entities;

public class Overtime
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid PayrollRunId { get; set; }
    public Guid EmployeeId { get; set; }

    public decimal Hours { get; set; }

    /// <summary>Rate per hour; prefilled from the employee master and editable per month.</summary>
    public decimal? Rate { get; set; }

    public string? Notes { get; set; }

    public PayrollRun PayrollRun { get; set; } = null!;
    public Employee Employee { get; set; } = null!;
}
