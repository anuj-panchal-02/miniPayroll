namespace MiniPayroll.Domain.Entities;

public class AuditLog
{
    public Guid Id { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid ActorUserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? Details { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
