using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Payroll.Statutory;

public interface IStatutoryRuleProvider
{
    PfRule PfFor(DateOnly on);
    EsiRule EsiFor(DateOnly on);
    bool ProfessionalTaxRequiresGender(string? companyState);
    decimal ProfessionalTax(string? companyState, Gender? gender, decimal wages, int month, DateOnly on);
    decimal LabourWelfareFund(string? companyState, int month, DateOnly on);
}
