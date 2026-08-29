using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Entities;

public class EmployeeSalaryComponent
{
    public Guid Id { get; set; }
    public Guid SalaryStructureId { get; set; }
    public Guid SalaryComponentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public SalaryComponentType Type { get; set; }
    public SalaryComponentValueType ValueType { get; set; }
    public decimal Value { get; set; }
    public int SortOrder { get; set; }

    public SalaryStructure SalaryStructure { get; set; } = null!;
    public SalaryComponent SalaryComponent { get; set; } = null!;
}
