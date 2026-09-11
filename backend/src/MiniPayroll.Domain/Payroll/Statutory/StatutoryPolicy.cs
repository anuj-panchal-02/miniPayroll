using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Payroll.Statutory;

public sealed record StatutoryPolicy(
    bool PfApplicable,
    bool PfUseWageCeiling,
    bool EsiApplicable,
    string? CompanyState,
    bool PfCovered,
    bool EsiCovered,
    Gender? Gender);

public sealed record StatutoryOverride(StatutoryKind Kind, decimal Amount);

public sealed record StatutoryLineResult(
    StatutoryKind Kind,
    decimal Computed,
    decimal Applied,
    decimal EmployerAmount);
