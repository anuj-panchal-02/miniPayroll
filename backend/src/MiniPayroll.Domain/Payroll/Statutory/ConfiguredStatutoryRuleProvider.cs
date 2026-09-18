using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Payroll.Statutory;

public sealed class ConfiguredStatutoryRuleProvider : IStatutoryRuleProvider
{
    public static ConfiguredStatutoryRuleProvider Instance { get; } = new();

    public PfRule PfFor(DateOnly on) => PfRules.For(on);

    public EsiRule EsiFor(DateOnly on) => EsiRules.For(on);

    public bool ProfessionalTaxRequiresGender(string? companyState) =>
        ProfessionalTaxSlabs.RequiresGender(companyState);

    public decimal ProfessionalTax(
        string? companyState,
        Gender? gender,
        decimal wages,
        int month,
        DateOnly on) =>
        ProfessionalTaxSlabs.AmountFor(companyState, gender, wages, month, on);

    public decimal LabourWelfareFund(string? companyState, int month, DateOnly on) =>
        LwfRules.AmountFor(companyState, month, on);
}
