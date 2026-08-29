using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Entities;

public class SalaryComponent
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public SalaryComponentType Type { get; set; }
    public bool IsStandardPreset { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Company Company { get; set; } = null!;
}
