namespace MiniPayroll.Domain.Payroll;

internal static class Proration
{
    public static decimal Ratio(int daysEmployed, int calendarDays) =>
        (decimal)daysEmployed / calendarDays;
}
