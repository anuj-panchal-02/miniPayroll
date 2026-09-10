using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Payroll;

public static class PayrollEligibility
{
    public static bool IsEligible(
        EmployeeStatus status,
        DateOnly? joiningDate,
        DateOnly? exitDate,
        PayrollPeriod period) => status switch
    {
        EmployeeStatus.Active =>
            joiningDate is null
            || (joiningDate <= period.LastDay
                && (exitDate is null || exitDate >= period.FirstDay)),
        EmployeeStatus.Inactive =>
            exitDate is { } exit && exit >= period.FirstDay && exit <= period.LastDay,
        _ => false
    };
}
