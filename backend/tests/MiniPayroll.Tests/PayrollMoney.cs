using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Payroll;

namespace MiniPayroll.Tests;

internal static class PayrollMoney
{
    public static void AssertLineTotals(PayrollEmployeeResult result)
    {
        var gross = result.Lines
            .Where(line => line.Type == SalaryComponentType.Earning)
            .Sum(line => line.Amount);
        var deductions = result.Lines
            .Where(line => line.Type == SalaryComponentType.Deduction)
            .Sum(line => line.Amount);

        Assert.Equal(gross, result.GrossEarnings);
        Assert.Equal(deductions, result.TotalDeductions);
        Assert.Equal(gross - deductions, result.NetSalary);
    }
}
