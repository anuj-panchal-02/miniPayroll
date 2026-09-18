using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Payroll.Statutory;

namespace MiniPayroll.Domain.Payroll;

public sealed record PayrollEmployeeInput(
    PayrollPeriod Period,
    DailyRateMethod DailyRateMethod,
    DateOnly? JoiningDate,
    DateOnly? ExitDate,
    PayrollAttendance Attendance,
    DateOnly? StructureEffectiveFrom,
    IReadOnlyList<PayrollStructureLine>? StructureLines,
    IReadOnlyList<PayrollOvertimeEntry>? Overtime = null,
    IReadOnlyList<PayrollAmountEntry>? Bonuses = null,
    IReadOnlyList<PayrollAmountEntry>? OneTimeDeductions = null,
    decimal? PreviousMonthNet = null,
    StatutoryPolicy? Statutory = null,
    IReadOnlyList<StatutoryOverride>? Overrides = null);

public enum PayrollLineKind
{
    RecurringEarning = 0,
    Overtime = 1,
    Bonus = 2,
    RecurringDeduction = 3,
    UnpaidLeave = 4,
    OneTimeDeduction = 5,
    Statutory = 6
}

public sealed record PayrollResultLine(
    string Name,
    SalaryComponentType Type,
    PayrollLineKind Kind,
    decimal Amount,
    StatutoryKind? StatutoryKind = null,
    decimal? ComputedAmount = null);

public sealed record PayrollEmployeeResult(
    IReadOnlyList<PayrollResultLine> Lines,
    decimal GrossEarnings,
    decimal TotalDeductions,
    decimal NetSalary,
    int DaysEmployed,
    decimal DailyRate,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    decimal EmployerPf = 0m,
    decimal EmployerEsi = 0m)
{
    public bool HasBlockingErrors => Errors.Count > 0;
}
