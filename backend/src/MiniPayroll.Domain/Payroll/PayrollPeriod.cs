using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Payroll.Statutory;

namespace MiniPayroll.Domain.Payroll;

public readonly record struct PayrollPeriod(int Year, int Month)
{
    public int CalendarDays => DateTime.DaysInMonth(Year, Month);
    public DateOnly FirstDay => new(Year, Month, 1);
    public DateOnly LastDay => new(Year, Month, CalendarDays);

    public bool IsValid => Year is >= 2000 and <= 2100 && Month is >= 1 and <= 12;

    public int? DaysEmployed(DateOnly? joiningDate, DateOnly? exitDate)
    {
        if (joiningDate is not { } joining)
        {
            return null;
        }

        if (joining > LastDay || exitDate < FirstDay)
        {
            return 0;
        }

        var employedFrom = joining > FirstDay ? joining : FirstDay;
        var employedTo = exitDate is { } exit && exit < LastDay ? exit : LastDay;
        return employedTo.DayNumber - employedFrom.DayNumber + 1;
    }
}

public sealed record PayrollAttendance(
    decimal WorkingDays,
    decimal Present,
    decimal PaidLeave,
    decimal UnpaidLeave);

public sealed record PayrollStructureLine(
    string Name,
    SalaryComponentType Type,
    SalaryComponentValueType ValueType,
    decimal Value,
    int SortOrder,
    SalaryComponentKind Kind = SalaryComponentKind.OtherEarning);

public sealed record PayrollOvertimeEntry(decimal Hours, decimal? Rate);

public sealed record PayrollAmountEntry(string Name, decimal Amount);
