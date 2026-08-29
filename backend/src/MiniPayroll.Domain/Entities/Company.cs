using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Entities;

public class Company
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? PostalCode { get; set; }
    public string? LogoPath { get; set; }
    public bool IsSetupComplete { get; set; }
    public CompanySetupStep SetupStep { get; set; } = CompanySetupStep.CompanyDetails;
    public DailyRateMethod DailyRateMethod { get; set; } = DailyRateMethod.CalendarDays;
    public int WorkingDaysPerMonth { get; set; } = 26;
    public string WeeklyOffDays { get; set; } = "Sunday";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ActivatedAt { get; set; }
    public byte[]? RowVersion { get; set; }

    public Subscription? Subscription { get; set; }
}
