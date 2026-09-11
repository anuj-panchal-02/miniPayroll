using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Entities;

public class PayrollRun
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public PayrollRunStatus Status { get; set; } = PayrollRunStatus.Draft;

    /// <summary>Daily-rate method snapshotted from the company when the run was created.</summary>
    public DailyRateMethod DailyRateMethod { get; set; }

    /// <summary>Company display name frozen at finalization for payslips.</summary>
    public string? CompanyName { get; set; }
    public string? CompanyLogoPath { get; set; }
    public string? CompanyAddress { get; set; }
    public string? PfEstablishmentCode { get; set; }
    public string? EsiCode { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CalculatedAt { get; set; }
    public DateTimeOffset? FinalizedAt { get; set; }
    public Guid? FinalizedByUserId { get; set; }
    public DateTimeOffset? ReversedAt { get; set; }
    public Guid? ReversedByUserId { get; set; }
    public string? ReversalReason { get; set; }
    public byte[]? RowVersion { get; set; }

    public Company Company { get; set; } = null!;
    public ICollection<MonthlyAttendance> Attendance { get; set; } = new List<MonthlyAttendance>();
    public ICollection<Overtime> Overtime { get; set; } = new List<Overtime>();
    public ICollection<Bonus> Bonuses { get; set; } = new List<Bonus>();
    public ICollection<Deduction> Deductions { get; set; } = new List<Deduction>();
    public ICollection<PayrollEmployee> Results { get; set; } = new List<PayrollEmployee>();
    public ICollection<PayrollStatutoryOverride> StatutoryOverrides { get; set; } = new List<PayrollStatutoryOverride>();
}
