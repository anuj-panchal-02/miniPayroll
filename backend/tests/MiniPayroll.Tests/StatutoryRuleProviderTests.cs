using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Payroll;
using MiniPayroll.Domain.Payroll.Statutory;

namespace MiniPayroll.Tests;

public class StatutoryRuleProviderTests
{
    private static readonly DateOnly On = new(2026, 8, 31);
    private static readonly IStatutoryRuleProvider Rules = ConfiguredStatutoryRuleProvider.Instance;

    [Fact]
    public void Pf_at_the_ceiling_is_eighteen_hundred_each()
    {
        var result = PfCalculator.Calculate(15000m, covered: true, useWageCeiling: true, On, Rules);
        Assert.Equal(1800m, result.Employee);
        Assert.Equal(1800m, result.Employer);
    }

    [Fact]
    public void Pf_one_rupee_above_the_ceiling_is_still_capped_at_eighteen_hundred()
    {
        var result = PfCalculator.Calculate(15001m, covered: true, useWageCeiling: true, On, Rules);
        Assert.Equal(1800m, result.Employee);
        Assert.Equal(1800m, result.Employer);
    }

    [Fact]
    public void Full_wage_pf_is_twelve_percent_of_actual_wages()
    {
        var result = PfCalculator.Calculate(20000m, covered: true, useWageCeiling: false, On, Rules);
        Assert.Equal(2400m, result.Employee);
        Assert.Equal(2400m, result.Employer);
    }

    [Fact]
    public void Pf_version_for_2026_is_the_2014_row()
    {
        var rule = Rules.PfFor(On);
        Assert.Equal(new DateOnly(2014, 9, 1), rule.EffectiveFrom);
        Assert.Null(rule.EffectiveTo);
        Assert.Equal(0.12m, rule.EmployeeRate);
        Assert.Equal(0.12m, rule.EmployerRate);
        Assert.Equal(15000m, rule.WageCeiling);
    }

    [Fact]
    public void Esi_at_the_eligibility_ceiling_still_contributes()
    {
        var result = EsiCalculator.Calculate(21000m, covered: true, On, Rules);
        Assert.Equal(158m, result.Employee);
        Assert.Equal(683m, result.Employer);
    }

    [Fact]
    public void Engine_warns_only_when_esi_wages_exceed_the_ceiling()
    {
        var atCeiling = PayrollCalculator.Calculate(EsiInput(21000m));
        Assert.Equal(158m, Assert.Single(atCeiling.Lines, line => line.StatutoryKind == StatutoryKind.EsiEmployee).Amount);
        Assert.DoesNotContain(PayrollCalculationMessages.EsiWagesAboveThreshold, atCeiling.Warnings);

        var above = PayrollCalculator.Calculate(EsiInput(21001m));
        Assert.Contains(PayrollCalculationMessages.EsiWagesAboveThreshold, above.Warnings);
    }

    [Theory]
    [InlineData("MH", Gender.Male, 200)]
    [InlineData("KA", null, 200)]
    [InlineData("WB", null, 200)]
    [InlineData("GJ", null, 200)]
    [InlineData("TN", null, 300)]
    [InlineData("AP", null, 200)]
    [InlineData("TS", null, 200)]
    [InlineData("KL", null, 1000)]
    [InlineData("OD", null, 200)]
    [InlineData("AS", null, 208)]
    public void Professional_tax_top_configured_slab(string state, Gender? gender, decimal amount)
    {
        Assert.Equal(amount, Rules.ProfessionalTax(state, gender, 200000m, 8, On));
    }

    [Fact]
    public void Maharashtra_female_top_slab_and_february_surcharge()
    {
        Assert.Equal(200m, Rules.ProfessionalTax("MH", Gender.Female, 200000m, 8, On));
        Assert.Equal(300m, Rules.ProfessionalTax("MH", Gender.Male, 200000m, 2, On));
        Assert.Equal(200m, Rules.ProfessionalTax("Maharashtra", Gender.Male, 30000m, 8, On));
        Assert.Equal(0m, Rules.ProfessionalTax("Maharashtra", Gender.Female, 24000m, 8, On));
    }

    [Fact]
    public void Professional_tax_is_zero_for_delhi_and_null_state()
    {
        Assert.Equal(0m, Rules.ProfessionalTax("Delhi", Gender.Male, 200000m, 8, On));
        Assert.Equal(0m, Rules.ProfessionalTax(null, Gender.Male, 200000m, 8, On));
    }

    [Fact]
    public void Professional_tax_requires_gender_only_for_maharashtra()
    {
        Assert.True(Rules.ProfessionalTaxRequiresGender("MH"));
        Assert.True(Rules.ProfessionalTaxRequiresGender("Maharashtra"));
        Assert.False(Rules.ProfessionalTaxRequiresGender("KA"));
        Assert.False(Rules.ProfessionalTaxRequiresGender("Delhi"));
        Assert.False(Rules.ProfessionalTaxRequiresGender(null));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(6)]
    [InlineData(12)]
    public void Maharashtra_lwf_is_twenty_five_every_month(int month)
    {
        Assert.Equal(25m, Rules.LabourWelfareFund("MH", month, On));
    }

    [Fact]
    public void Karnataka_lwf_is_twenty_every_month()
    {
        Assert.Equal(20m, Rules.LabourWelfareFund("KA", 8, On));
    }

    [Fact]
    public void Kerala_lwf_is_fifty_in_june_and_december_and_zero_in_august()
    {
        Assert.Equal(50m, Rules.LabourWelfareFund("KL", 6, On));
        Assert.Equal(50m, Rules.LabourWelfareFund("KL", 12, On));
        Assert.Equal(0m, Rules.LabourWelfareFund("KL", 8, On));
    }

    [Fact]
    public void Unlisted_state_lwf_is_zero()
    {
        Assert.Equal(0m, Rules.LabourWelfareFund("Delhi", 8, On));
        Assert.Equal(0m, Rules.LabourWelfareFund(null, 8, On));
    }

    private static PayrollEmployeeInput EsiInput(decimal basic) => new(
        new PayrollPeriod(2026, 8),
        DailyRateMethod.CalendarDays,
        new DateOnly(2025, 1, 1),
        null,
        new PayrollAttendance(26, 26, 0, 0),
        new DateOnly(2025, 1, 1),
        [new PayrollStructureLine("Basic Salary", SalaryComponentType.Earning, SalaryComponentValueType.FixedAmount, basic, 0)],
        null,
        null,
        null,
        null,
        new StatutoryPolicy(false, true, true, "Delhi", false, true, Gender.Male),
        null);
}
