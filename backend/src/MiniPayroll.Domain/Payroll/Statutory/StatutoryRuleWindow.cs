namespace MiniPayroll.Domain.Payroll.Statutory;

internal static class StatutoryRuleWindow
{
    public static bool Covers(DateOnly on, DateOnly? effectiveFrom, DateOnly? effectiveTo) =>
        (effectiveFrom is null || effectiveFrom <= on)
        && (effectiveTo is null || on <= effectiveTo);
}
