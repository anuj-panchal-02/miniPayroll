namespace MiniPayroll.Domain.Payroll.Statutory;

public readonly record struct PfRule(DateOnly EffectiveFrom, decimal EmployeeRate, decimal EmployerRate, decimal WageCeiling);

public static class PfRules
{
    public static readonly IReadOnlyList<PfRule> All =
    [
        new(new DateOnly(2014, 9, 1), 0.12m, 0.12m, 15000m)
    ];

    public static PfRule For(DateOnly on)
    {
        var match = All.LastOrDefault(rule => rule.EffectiveFrom <= on);
        return match.EffectiveFrom == default ? All[0] : match;
    }
}

public readonly record struct EsiRule(
    DateOnly EffectiveFrom,
    decimal EmployeeRate,
    decimal EmployerRate,
    decimal EligibilityCeiling);

public static class EsiRules
{
    public static readonly IReadOnlyList<EsiRule> All =
    [
        new(new DateOnly(2019, 7, 1), 0.0075m, 0.0325m, 21000m)
    ];

    public static EsiRule For(DateOnly on)
    {
        var match = All.LastOrDefault(rule => rule.EffectiveFrom <= on);
        return match.EffectiveFrom == default ? All[0] : match;
    }
}
