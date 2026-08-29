using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Entities;

public class Employee
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public string? Department { get; set; }
    public EmploymentType EmploymentType { get; set; } = EmploymentType.FullTimeMonthly;
    public DateOnly? JoiningDate { get; set; }
    public DateOnly? ExitDate { get; set; }
    public EmployeeStatus Status { get; set; } = EmployeeStatus.Active;
    public int? DraftStep { get; set; }
    public string BankName { get; set; } = string.Empty;
    public string BankAccountNumber { get; set; } = string.Empty;
    public string Ifsc { get; set; } = string.Empty;
    public string? UpiId { get; set; }
    public decimal? OvertimeRate { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Company Company { get; set; } = null!;
}
