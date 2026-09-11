using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Payroll.Statutory;

public readonly record struct ProfessionalTaxSlab(
    string StateCode,
    Gender? Gender,
    decimal MaxSalaryExclusive,
    decimal Amount,
    int? OnlyInMonth);

public static class ProfessionalTaxSlabs
{
    public static readonly IReadOnlyList<ProfessionalTaxSlab> All =
    [
        new("MH", Gender.Male, 7501m, 0m, null),
        new("MH", Gender.Male, 10001m, 175m, null),
        new("MH", Gender.Male, decimal.MaxValue, 200m, null),
        new("MH", Gender.Female, 25001m, 0m, null),
        new("MH", Gender.Female, decimal.MaxValue, 200m, null),
        new("KA", null, 25001m, 0m, null),
        new("KA", null, decimal.MaxValue, 200m, null),
        new("WB", null, 10001m, 0m, null),
        new("WB", null, 15001m, 110m, null),
        new("WB", null, 25001m, 130m, null),
        new("WB", null, 40001m, 150m, null),
        new("WB", null, decimal.MaxValue, 200m, null),
        new("GJ", null, 12001m, 0m, null),
        new("GJ", null, decimal.MaxValue, 200m, null),
        new("TN", null, 21001m, 0m, null),
        new("TN", null, 30001m, 135m, null),
        new("TN", null, 45001m, 180m, null),
        new("TN", null, 60001m, 220m, null),
        new("TN", null, 75001m, 250m, null),
        new("TN", null, decimal.MaxValue, 300m, null),
        new("AP", null, 15001m, 0m, null),
        new("AP", null, 20001m, 150m, null),
        new("AP", null, decimal.MaxValue, 200m, null),
        new("TS", null, 15001m, 0m, null),
        new("TS", null, 20001m, 150m, null),
        new("TS", null, decimal.MaxValue, 200m, null),
        new("KL", null, 12000m, 0m, null),
        new("KL", null, 18000m, 120m, null),
        new("KL", null, 30000m, 180m, null),
        new("KL", null, 45000m, 300m, null),
        new("KL", null, 60000m, 450m, null),
        new("KL", null, 75000m, 600m, null),
        new("KL", null, 100000m, 750m, null),
        new("KL", null, decimal.MaxValue, 1000m, null),
        new("OD", null, 13305m, 0m, null),
        new("OD", null, 25001m, 125m, null),
        new("OD", null, decimal.MaxValue, 200m, null),
        new("AS", null, 10001m, 0m, null),
        new("AS", null, 15001m, 150m, null),
        new("AS", null, 25001m, 180m, null),
        new("AS", null, decimal.MaxValue, 208m, null)
    ];

    public static decimal AmountFor(string? companyState, Gender? gender, decimal salary, int month)
    {
        var code = IndianStateCatalog.CodeFor(companyState);
        if (code is null)
        {
            return 0m;
        }

        var slabs = All.Where(slab =>
                slab.StateCode == code
                && (slab.Gender is null || slab.Gender == gender)
                && (slab.OnlyInMonth is null || slab.OnlyInMonth == month))
            .OrderBy(slab => slab.MaxSalaryExclusive)
            .ToList();
        if (slabs.Count == 0)
        {
            return 0m;
        }

        var match = slabs.FirstOrDefault(slab => salary < slab.MaxSalaryExclusive);
        var amount = match.StateCode is null ? slabs[^1].Amount : match.Amount;
        if (code == "MH" && month == 2 && amount == 200m)
        {
            return 300m;
        }

        return amount;
    }
}
