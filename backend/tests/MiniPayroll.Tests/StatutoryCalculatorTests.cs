using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Payroll;
using MiniPayroll.Domain.Payroll.Statutory;

namespace MiniPayroll.Tests;

public class StatutoryCalculatorTests
{
    private static readonly DateOnly On = new(2026, 8, 31);

    [Fact]
    public void Pf_uses_twelve_percent_and_the_wage_ceiling()
    {
        var capped = PfCalculator.Calculate(20000m, covered: true, useWageCeiling: true, On);
        Assert.Equal(1800m, capped.Employee);
        Assert.Equal(1800m, capped.Employer);

        var full = PfCalculator.Calculate(20000m, covered: true, useWageCeiling: false, On);
        Assert.Equal(2400m, full.Employee);
    }

    [Fact]
    public void Pf_is_zero_when_not_covered()
    {
        var result = PfCalculator.Calculate(15000m, covered: false, useWageCeiling: true, On);
        Assert.Equal(0m, result.Employee);
        Assert.Equal(0m, result.Employer);
    }

    [Fact]
    public void Esi_uses_point_seven_five_and_three_point_two_five()
    {
        var result = EsiCalculator.Calculate(18000m, covered: true, On);
        Assert.Equal(135m, result.Employee);
        Assert.Equal(585m, result.Employer);
    }

    [Fact]
    public void Esi_continues_above_the_eligibility_band_when_covered()
    {
        var result = EsiCalculator.Calculate(25000m, covered: true, On);
        Assert.Equal(188m, result.Employee);
        Assert.Equal(813m, result.Employer);
    }

    [Fact]
    public void Maharashtra_professional_tax_uses_gender_slabs()
    {
        Assert.Equal(200m, ProfessionalTaxCalculator.Calculate("Maharashtra", Gender.Male, 30000m, 8));
        Assert.Equal(0m, ProfessionalTaxCalculator.Calculate("Maharashtra", Gender.Female, 24000m, 8));
        Assert.Equal(200m, ProfessionalTaxCalculator.Calculate("Maharashtra", Gender.Female, 26000m, 8));
        Assert.Equal(300m, ProfessionalTaxCalculator.Calculate("MH", Gender.Male, 30000m, 2));
    }

    [Fact]
    public void Lwf_skips_months_without_a_contribution()
    {
        Assert.Equal(50m, LwfCalculator.Calculate("Kerala", 6));
        Assert.Equal(0m, LwfCalculator.Calculate("Kerala", 8));
        Assert.Equal(25m, LwfCalculator.Calculate("Maharashtra", 8));
    }

    [Fact]
    public void Override_replaces_the_computed_amount_and_keeps_employer_cost()
    {
        var policy = new StatutoryPolicy(true, true, true, "Maharashtra", true, true, Gender.Male);
        var result = StatutoryCalculator.Calculate(
            policy, On, 8, pfWages: 15000m, esiWages: 18000m, ptWages: 28000m,
            [new StatutoryOverride(StatutoryKind.PfEmployee, 0m)]);

        var pf = Assert.Single(result, line => line.Kind == StatutoryKind.PfEmployee);
        Assert.Equal(1800m, pf.Computed);
        Assert.Equal(0m, pf.Applied);
        Assert.Equal(1800m, pf.EmployerAmount);
    }
}

public class StatutoryPayrollCalculatorTests
{
    private static readonly PayrollPeriod August = new(2026, 8);
    private static readonly DateOnly LongAgo = new(2025, 1, 1);

    private static readonly IReadOnlyList<PayrollStructureLine> BasicPlusHra =
    [
        new("Basic Salary", SalaryComponentType.Earning, SalaryComponentValueType.FixedAmount, 20000m, 0),
        new("HRA", SalaryComponentType.Earning, SalaryComponentValueType.PercentageOfBasic, 40m, 1),
    ];

    private static PayrollEmployeeInput Input(
        StatutoryPolicy? statutory = null,
        DateOnly? joining = null,
        PayrollAttendance? attendance = null,
        IReadOnlyList<PayrollOvertimeEntry>? overtime = null,
        IReadOnlyList<PayrollAmountEntry>? bonuses = null,
        IReadOnlyList<StatutoryOverride>? overrides = null,
        IReadOnlyList<PayrollStructureLine>? structure = null) => new(
        August,
        DailyRateMethod.CalendarDays,
        joining ?? LongAgo,
        null,
        attendance ?? new PayrollAttendance(26, 26, 0, 0),
        LongAgo,
        structure ?? BasicPlusHra,
        overtime,
        bonuses,
        null,
        null,
        statutory ?? new StatutoryPolicy(true, true, false, "Maharashtra", true, false, Gender.Male),
        overrides);

    [Fact]
    public void Ceiling_pf_is_eighteen_hundred_on_basic_above_the_cap()
    {
        var result = PayrollCalculator.Calculate(Input());

        var pf = Assert.Single(result.Lines, line => line.StatutoryKind == StatutoryKind.PfEmployee);
        Assert.Equal(1800m, pf.Amount);
        Assert.Equal(1800m, result.EmployerPf);
        Assert.Equal(200m, Assert.Single(result.Lines, line => line.StatutoryKind == StatutoryKind.ProfessionalTax).Amount);
        Assert.Equal(25m, Assert.Single(result.Lines, line => line.StatutoryKind == StatutoryKind.LwfEmployee).Amount);
    }

    [Fact]
    public void Full_wage_pf_uses_uncapped_basic()
    {
        var result = PayrollCalculator.Calculate(Input(
            statutory: new StatutoryPolicy(true, false, false, "Delhi", true, false, Gender.Male)));

        Assert.Equal(2400m, Assert.Single(result.Lines, line => line.StatutoryKind == StatutoryKind.PfEmployee).Amount);
        Assert.DoesNotContain(result.Lines, line => line.StatutoryKind == StatutoryKind.ProfessionalTax);
    }

    [Fact]
    public void Esi_includes_overtime_and_excludes_bonus()
    {
        var result = PayrollCalculator.Calculate(Input(
            statutory: new StatutoryPolicy(false, true, true, "Delhi", false, true, Gender.Male),
            overtime: [new PayrollOvertimeEntry(10m, 100m)],
            bonuses: [new PayrollAmountEntry("Festival Bonus", 5000m)]));

        // ESI wages = 28000 + 1000 OT = 29000; 0.75% → 218
        var esi = Assert.Single(result.Lines, line => line.StatutoryKind == StatutoryKind.EsiEmployee);
        Assert.Equal(218m, esi.Amount);
        Assert.Equal(943m, result.EmployerEsi);
        Assert.Contains(PayrollCalculationMessages.EsiWagesAboveThreshold, result.Warnings);
    }

    [Fact]
    public void Mid_month_join_computes_pf_on_prorated_pf_wages_and_keeps_full_pt()
    {
        var result = PayrollCalculator.Calculate(Input(joining: new DateOnly(2026, 8, 16)));

        var pf = Assert.Single(result.Lines, line => line.StatutoryKind == StatutoryKind.PfEmployee);
        Assert.Equal(1239m, pf.Amount); // Basic 10323 × 12%
        Assert.Equal(200m, Assert.Single(result.Lines, line => line.StatutoryKind == StatutoryKind.ProfessionalTax).Amount);
        Assert.Contains(PayrollCalculationMessages.Prorated, result.Warnings);
    }

    [Fact]
    public void Unpaid_leave_reduces_pf_wages_before_twelve_percent()
    {
        var result = PayrollCalculator.Calculate(Input(
            attendance: new PayrollAttendance(26, 25, 0, 1)));

        var unpaid = Assert.Single(result.Lines, line => line.Kind == PayrollLineKind.UnpaidLeave);
        Assert.Equal(903m, unpaid.Amount); // 28000/31
        var pf = Assert.Single(result.Lines, line => line.StatutoryKind == StatutoryKind.PfEmployee);
        // PF wages = 20000 - 903 × 20000/28000 = 19355.00 → 12% capped = 1800 still (above 15k)
        Assert.Equal(1800m, pf.Amount);
    }

    [Fact]
    public void Override_survives_as_applied_amount()
    {
        var result = PayrollCalculator.Calculate(Input(
            overrides: [new StatutoryOverride(StatutoryKind.PfEmployee, 0m)]));

        var pf = Assert.Single(result.Lines, line => line.StatutoryKind == StatutoryKind.PfEmployee);
        Assert.Equal(0m, pf.Amount);
        Assert.Equal(1800m, pf.ComputedAmount);
        Assert.Contains(PayrollCalculationMessages.StatutoryOverrideApplied, result.Warnings);
        Assert.Equal(1800m, result.EmployerPf);
    }
}
