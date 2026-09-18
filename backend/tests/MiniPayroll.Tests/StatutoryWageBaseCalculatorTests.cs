using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Payroll;

namespace MiniPayroll.Tests;

public class StatutoryWageBaseCalculatorTests
{
    [Fact]
    public void One_unpaid_day_reduces_pf_esi_and_pt_from_basic_plus_hra()
    {
        var structure = new List<PayrollStructureLine>
        {
            new("Basic Salary", SalaryComponentType.Earning, SalaryComponentValueType.FixedAmount, 20000m, 0),
            new("HRA", SalaryComponentType.Earning, SalaryComponentValueType.PercentageOfBasic, 40m, 1),
        };
        var lines = new List<PayrollResultLine>
        {
            new("Basic Salary", SalaryComponentType.Earning, PayrollLineKind.RecurringEarning, 20000m),
            new("HRA", SalaryComponentType.Earning, PayrollLineKind.RecurringEarning, 8000m),
            new("Unpaid Leave", SalaryComponentType.Deduction, PayrollLineKind.UnpaidLeave, 903m),
        };

        var bases = StatutoryWageBaseCalculator.Calculate(structure, lines, unpaidLeaveAmount: 903m);

        Assert.Equal(19355.00m, bases.PfWages);
        Assert.Equal(27097.00m, bases.EsiWages);
        Assert.Equal(27097m, bases.PtWages);
    }
}
