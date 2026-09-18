using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Payroll;
using MiniPayroll.Domain.Payroll.Statutory;

namespace MiniPayroll.Tests;

/// <summary>
/// Professional Tax is a slab on (earning lines − unpaid leave), not a
/// full-month CTC amount. LWF for Maharashtra is a fixed ₹25 in August
/// and is asserted separately.
/// </summary>
public class ProfessionalTaxWageBaseTests
{
    private static readonly PayrollPeriod August = new(2026, 8);
    private static readonly DateOnly LongAgo = new(2025, 1, 1);
    private static readonly StatutoryPolicy MaharashtraMaleNoPfEsi = new(
        false, true, false, "Maharashtra", false, false, Gender.Male);

    private static readonly IReadOnlyList<PayrollStructureLine> BasicPlusHra =
    [
        new("Basic Salary", SalaryComponentType.Earning, SalaryComponentValueType.FixedAmount, 20000m, 0),
        new("HRA", SalaryComponentType.Earning, SalaryComponentValueType.PercentageOfBasic, 40m, 1),
    ];

    [Fact]
    public void Full_month_employee_pays_two_hundred_pt_on_28000_earnings()
    {
        var result = Calculate();

        Assert.Equal(28000m, result.GrossEarnings);
        Assert.Equal(200m, Pt(result));
        Assert.Equal(25m, Lwf(result));
        Assert.Equal(27775m, result.NetSalary);
        PayrollMoney.AssertLineTotals(result);
    }

    [Fact]
    public void One_day_joiner_pays_zero_pt_because_the_wage_base_is_prorated_earnings()
    {
        var result = Calculate(joining: new DateOnly(2026, 8, 31));

        Assert.Equal(1, result.DaysEmployed);
        Assert.Equal(645m, result.Lines.Single(line => line.Name == "Basic Salary").Amount);
        Assert.Equal(258m, result.Lines.Single(line => line.Name == "HRA").Amount);
        Assert.Equal(903m, result.GrossEarnings);
        Assert.Equal(0m, Pt(result));
        Assert.DoesNotContain(result.Lines, line => line.StatutoryKind == StatutoryKind.ProfessionalTax);
        Assert.Equal(25m, Lwf(result));
        Assert.Equal(878m, result.NetSalary);
        Assert.Contains(PayrollCalculationMessages.Prorated, result.Warnings);
        PayrollMoney.AssertLineTotals(result);
    }

    [Fact]
    public void One_day_exiter_pays_zero_pt_on_the_same_prorated_wage_base()
    {
        var result = Calculate(exit: new DateOnly(2026, 8, 1));

        Assert.Equal(1, result.DaysEmployed);
        Assert.Equal(903m, result.GrossEarnings);
        Assert.Equal(0m, Pt(result));
        Assert.DoesNotContain(result.Lines, line => line.StatutoryKind == StatutoryKind.ProfessionalTax);
        Assert.Equal(25m, Lwf(result));
        Assert.Equal(878m, result.NetSalary);
        Assert.Contains(PayrollCalculationMessages.Prorated, result.Warnings);
        PayrollMoney.AssertLineTotals(result);
    }

    [Fact]
    public void Mid_month_joiner_on_16_aug_hits_the_200_pt_slab_on_prorated_14452()
    {
        var result = Calculate(joining: new DateOnly(2026, 8, 16));

        Assert.Equal(16, result.DaysEmployed);
        Assert.Equal(14452m, result.GrossEarnings);
        Assert.Equal(200m, Pt(result));
        Assert.Equal(25m, Lwf(result));
        Assert.Equal(14227m, result.NetSalary);
        PayrollMoney.AssertLineTotals(result);
    }

    [Fact]
    public void Heavy_lop_of_26_days_drops_pt_to_zero_because_wage_base_subtracts_unpaid_leave()
    {
        var result = Calculate(attendance: new PayrollAttendance(26, 0, 0, 26));

        Assert.Equal(23484m, Assert.Single(result.Lines, line => line.Kind == PayrollLineKind.UnpaidLeave).Amount);
        Assert.Equal(28000m, result.GrossEarnings);
        Assert.Equal(0m, Pt(result));
        Assert.DoesNotContain(result.Lines, line => line.StatutoryKind == StatutoryKind.ProfessionalTax);
        Assert.Equal(25m, Lwf(result));
        Assert.Equal(4491m, result.NetSalary);
        Assert.Contains(PayrollCalculationMessages.HighUnpaidLeave, result.Warnings);
        PayrollMoney.AssertLineTotals(result);
    }

    [Fact]
    public void Overtime_is_included_in_the_pt_wage_base()
    {
        var withoutOt = Calculate(joining: new DateOnly(2026, 8, 22));
        var withOt = Calculate(
            joining: new DateOnly(2026, 8, 22),
            overtime: [new PayrollOvertimeEntry(10m, 150m)]);

        Assert.Equal(9033m, withoutOt.GrossEarnings);
        Assert.Equal(175m, Pt(withoutOt));
        Assert.Equal(10533m, withOt.GrossEarnings);
        Assert.Equal(200m, Pt(withOt));
        PayrollMoney.AssertLineTotals(withoutOt);
        PayrollMoney.AssertLineTotals(withOt);
    }

    [Fact]
    public void Bonus_is_included_in_the_pt_wage_base()
    {
        var withoutBonus = Calculate(joining: new DateOnly(2026, 8, 22));
        var withBonus = Calculate(
            joining: new DateOnly(2026, 8, 22),
            bonuses: [new PayrollAmountEntry("Festival Bonus", 2000m)]);

        Assert.Equal(175m, Pt(withoutBonus));
        Assert.Equal(11033m, withBonus.GrossEarnings);
        Assert.Equal(200m, Pt(withBonus));
        PayrollMoney.AssertLineTotals(withBonus);
    }

    [Fact]
    public void Combined_ot_bonus_and_lop_uses_earnings_minus_unpaid_leave_as_pt_wages()
    {
        var result = Calculate(
            overtime: [new PayrollOvertimeEntry(10m, 150m)],
            bonuses: [new PayrollAmountEntry("Festival Bonus", 5000m)],
            attendance: new PayrollAttendance(26, 25, 0, 1));

        Assert.Equal(1500m, Assert.Single(result.Lines, line => line.Kind == PayrollLineKind.Overtime).Amount);
        Assert.Equal(5000m, Assert.Single(result.Lines, line => line.Kind == PayrollLineKind.Bonus).Amount);
        Assert.Equal(903m, Assert.Single(result.Lines, line => line.Kind == PayrollLineKind.UnpaidLeave).Amount);
        Assert.Equal(34500m, result.GrossEarnings);
        Assert.Equal(200m, Pt(result));
        Assert.Equal(25m, Lwf(result));
        Assert.Equal(33372m, result.NetSalary);
        PayrollMoney.AssertLineTotals(result);
    }

    private static PayrollEmployeeResult Calculate(
        DateOnly? joining = null,
        DateOnly? exit = null,
        PayrollAttendance? attendance = null,
        IReadOnlyList<PayrollOvertimeEntry>? overtime = null,
        IReadOnlyList<PayrollAmountEntry>? bonuses = null) =>
        PayrollCalculator.Calculate(new PayrollEmployeeInput(
            August,
            DailyRateMethod.CalendarDays,
            joining ?? LongAgo,
            exit,
            attendance ?? new PayrollAttendance(26, 26, 0, 0),
            LongAgo,
            BasicPlusHra,
            overtime,
            bonuses,
            Statutory: MaharashtraMaleNoPfEsi));

    private static decimal Pt(PayrollEmployeeResult result) =>
        result.Lines.SingleOrDefault(line => line.StatutoryKind == StatutoryKind.ProfessionalTax)?.Amount ?? 0m;

    private static decimal Lwf(PayrollEmployeeResult result) =>
        result.Lines.Single(line => line.StatutoryKind == StatutoryKind.LwfEmployee).Amount;
}
