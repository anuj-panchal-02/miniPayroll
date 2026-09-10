namespace MiniPayroll.Domain.Payroll;

public static class PayrollInputRules
{
    public const decimal MaxDays = 31m;
    public const int MaxNotesLength = 500;

    public static bool IsHalfDayQuantity(decimal value) =>
        value >= 0m
        && value <= MaxDays
        && value * 2m == decimal.Truncate(value * 2m);

    public static bool IsPositiveAmount(decimal value) => value > 0m;

    public static bool IsValidNotes(string? notes) =>
        notes is null || notes.Length <= MaxNotesLength;
}
