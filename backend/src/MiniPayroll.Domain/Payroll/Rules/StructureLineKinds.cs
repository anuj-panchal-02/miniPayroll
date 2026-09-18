using MiniPayroll.Domain.Payroll.Statutory;

namespace MiniPayroll.Domain.Payroll;

internal static class StructureLineKinds
{
    public static SalaryComponentKind Resolve(PayrollStructureLine line) =>
        line.Kind == SalaryComponentKind.OtherEarning
            ? SalaryComponentKinds.FromName(line.Name)
            : line.Kind;
}
