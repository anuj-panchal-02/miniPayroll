namespace MiniPayroll.Domain.Constants;

public static class AuditActions
{
    public const string CompanySetupComplete = "company.setup.complete";
    public const string EmployeeCreate = "employee.create";
    public const string EmployeeUpdate = "employee.update";
    public const string EmployeeDeactivate = "employee.deactivate";
    public const string SalaryStructureCreate = "salary-structure.create";
    public const string PayrollRunCreate = "payroll.run.create";
    public const string PayrollRunCalculate = "payroll.run.calculate";
    public const string PayrollInputsSave = "payroll.inputs.save";
}
