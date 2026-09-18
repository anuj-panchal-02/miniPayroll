namespace MiniPayroll.Domain.Payroll;

public static class PayrollInputRules
{
    public const decimal MaxDays = 31m;
    public const int MaxNotesLength = 500;

    public static bool IsHalfDayQuantity(decimal value) =>
        IsHalfDayQuantity(value, MaxDays);

    public static bool IsHalfDayQuantity(decimal value, decimal maxDays) =>
        value >= 0m
        && value <= maxDays
        && value * 2m == decimal.Truncate(value * 2m);

    public static bool IsWithinMonthBounds(
        decimal workingDays,
        decimal present,
        decimal paidLeave,
        decimal unpaidLeave,
        PayrollPeriod period,
        DateOnly? joiningDate,
        DateOnly? exitDate)
    {
        var calendarDays = period.CalendarDays;
        if (!IsHalfDayQuantity(workingDays, calendarDays)
            || !IsHalfDayQuantity(present, calendarDays)
            || !IsHalfDayQuantity(paidLeave, calendarDays)
            || !IsHalfDayQuantity(unpaidLeave, calendarDays))
        {
            return false;
        }

        if (unpaidLeave > workingDays)
        {
            return false;
        }

        var daysEmployed = period.DaysEmployed(joiningDate, exitDate);
        return daysEmployed is not int employed || unpaidLeave <= employed;
    }

    public static bool IsPositiveAmount(decimal value) => value > 0m;

    public static bool IsValidNotes(string? notes) =>
        notes is null || notes.Length <= MaxNotesLength;
}
