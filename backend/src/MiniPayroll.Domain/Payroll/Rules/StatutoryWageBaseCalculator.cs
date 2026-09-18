using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Payroll.Statutory;

namespace MiniPayroll.Domain.Payroll;

public readonly record struct StatutoryWageBases(decimal PfWages, decimal EsiWages, decimal PtWages);

public static class StatutoryWageBaseCalculator
{
    public static StatutoryWageBases Calculate(
        IReadOnlyList<PayrollStructureLine> earningLines,
        IReadOnlyList<PayrollResultLine> lines,
        decimal unpaidLeaveAmount)
    {
        var recurring = lines.Where(line => line.Kind == PayrollLineKind.RecurringEarning).ToList();
        var totalRecurring = recurring.Sum(line => line.Amount);
        decimal Reduce(PayrollResultLine line)
        {
            if (totalRecurring <= 0 || unpaidLeaveAmount <= 0)
            {
                return line.Amount;
            }

            return Math.Max(0m, line.Amount - unpaidLeaveAmount * line.Amount / totalRecurring);
        }

        var pfWages = 0m;
        var esiWages = 0m;
        foreach (var line in recurring)
        {
            var reduced = Reduce(line);
            esiWages += reduced;
            var structure = earningLines.FirstOrDefault(item => item.Name == line.Name);
            var kind = structure is null
                ? SalaryComponentKinds.FromName(line.Name)
                : StructureLineKinds.Resolve(structure);
            if (kind is SalaryComponentKind.Basic or SalaryComponentKind.Da)
            {
                pfWages += reduced;
            }
        }

        esiWages = PayrollMoney.TwoDecimals(esiWages)
            + lines.Where(line => line.Kind == PayrollLineKind.Overtime).Sum(line => line.Amount);
        var ptWages = lines.Where(line => line.Type == SalaryComponentType.Earning).Sum(line => line.Amount)
            - unpaidLeaveAmount;
        if (ptWages < 0)
        {
            ptWages = 0;
        }

        return new StatutoryWageBases(
            PayrollMoney.TwoDecimals(pfWages),
            esiWages,
            ptWages);
    }
}
