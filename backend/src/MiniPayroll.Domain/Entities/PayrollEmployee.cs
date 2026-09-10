using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Entities;

/// <summary>Per-employee calculation snapshot for a payroll run. Never recomputed from live masters.</summary>
public class PayrollEmployee
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid PayrollRunId { get; set; }
    public Guid EmployeeId { get; set; }

    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;

    public int DaysEmployed { get; set; }
    public decimal DailyRate { get; set; }
    public decimal GrossEarnings { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal NetSalary { get; set; }

    /// <summary>Newline-separated non-blocking warnings persisted with the calculation.</summary>
    public string? Warnings { get; set; }

    /// <summary>Newline-separated blocking errors; non-null keeps the run in Draft.</summary>
    public string? Errors { get; set; }

    public SalaryPaymentStatus PaymentStatus { get; set; } = SalaryPaymentStatus.Unpaid;
    public SalaryPaymentMode? PaymentMode { get; set; }
    public DateOnly? PaidOn { get; set; }
    public string? PaymentReference { get; set; }

    public PayrollRun PayrollRun { get; set; } = null!;
    public Employee Employee { get; set; } = null!;
    public ICollection<PayrollEarning> Earnings { get; set; } = new List<PayrollEarning>();
    public ICollection<PayrollDeduction> Deductions { get; set; } = new List<PayrollDeduction>();
}
