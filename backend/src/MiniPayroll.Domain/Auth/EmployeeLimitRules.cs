using MiniPayroll.Domain.Constants;

namespace MiniPayroll.Domain.Auth;

public static class EmployeeLimitRules
{
    public static bool IsValid(int employeeLimit) =>
        employeeLimit >= PlatformLimits.MinEmployeeLimit
        && employeeLimit <= PlatformLimits.HardEmployeeCap;

    public static string InvalidMessage =>
        $"Employee limit must be between {PlatformLimits.MinEmployeeLimit} and {PlatformLimits.HardEmployeeCap}.";
}
