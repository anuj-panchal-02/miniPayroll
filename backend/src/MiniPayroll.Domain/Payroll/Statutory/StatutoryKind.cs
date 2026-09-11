namespace MiniPayroll.Domain.Payroll.Statutory;

public enum StatutoryKind
{
    PfEmployee = 0,
    EsiEmployee = 1,
    ProfessionalTax = 2,
    LwfEmployee = 3
}

public static class StatutoryLabels
{
    public static string Name(StatutoryKind kind) => kind switch
    {
        StatutoryKind.PfEmployee => "Provident Fund (PF)",
        StatutoryKind.EsiEmployee => "ESI",
        StatutoryKind.ProfessionalTax => "Professional Tax",
        StatutoryKind.LwfEmployee => "Labour Welfare Fund (LWF)",
        _ => kind.ToString()
    };
}
