namespace MiniPayroll.Domain.Entities;

public class MonthlyAttendance
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid PayrollRunId { get; set; }
    public Guid EmployeeId { get; set; }

    public decimal WorkingDays { get; set; }
    public decimal Present { get; set; }
    public decimal PaidLeave { get; set; }
    public decimal UnpaidLeave { get; set; }

    public PayrollRun PayrollRun { get; set; } = null!;
    public Employee Employee { get; set; } = null!;
}
