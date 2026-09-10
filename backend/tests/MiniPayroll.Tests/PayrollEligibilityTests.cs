using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Payroll;

namespace MiniPayroll.Tests;

public class PayrollEligibilityTests
{
    private static readonly PayrollPeriod August = new(2026, 8);

    [Fact]
    public void Active_employees_are_included_unless_outside_the_period()
    {
        Assert.True(PayrollEligibility.IsEligible(
            EmployeeStatus.Active, new DateOnly(2026, 1, 1), null, August));
        Assert.True(PayrollEligibility.IsEligible(
            EmployeeStatus.Active, null, null, August));
        Assert.False(PayrollEligibility.IsEligible(
            EmployeeStatus.Active, new DateOnly(2026, 9, 1), null, August));
        Assert.False(PayrollEligibility.IsEligible(
            EmployeeStatus.Active, new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 31), August));
    }

    [Fact]
    public void Inactive_employees_are_included_only_in_their_exit_month()
    {
        Assert.True(PayrollEligibility.IsEligible(
            EmployeeStatus.Inactive, new DateOnly(2026, 1, 1), new DateOnly(2026, 8, 10), August));
        Assert.False(PayrollEligibility.IsEligible(
            EmployeeStatus.Inactive, new DateOnly(2026, 1, 1), new DateOnly(2026, 7, 31), August));
        Assert.False(PayrollEligibility.IsEligible(
            EmployeeStatus.Draft, new DateOnly(2026, 1, 1), null, August));
    }
}
