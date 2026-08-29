using MiniPayroll.Domain.Constants;

namespace MiniPayroll.Domain.Auth;

public static class EmployeeLimitRules
{
    public static bool IsValid(int employeeLimit) =>
        employeeLimit is >= 1 and <= PlatformLimits.HardEmployeeCap;

    public static string InvalidMessage =>
        $"Employee limit must be between 1 and {PlatformLimits.HardEmployeeCap}.";
}
