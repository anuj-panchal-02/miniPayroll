namespace MiniPayroll.Domain.Entities;

public class SalaryStructure
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid EmployeeId { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Company Company { get; set; } = null!;
    public Employee Employee { get; set; } = null!;
    public ICollection<EmployeeSalaryComponent> Components { get; set; } = [];
}
