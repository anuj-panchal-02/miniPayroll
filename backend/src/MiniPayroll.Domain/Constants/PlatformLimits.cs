namespace MiniPayroll.Domain.Constants;

/// <summary>
/// Product-wide limits. Raise <see cref="HardEmployeeCap"/> when payroll should support more employees;
/// <see cref="DefaultEmployeeLimit"/> follows unless you set it independently.
/// </summary>
public static class PlatformLimits
{
    public const int MinEmployeeLimit = 1;
    public const int HardEmployeeCap = 50;
    public const int DefaultEmployeeLimit = HardEmployeeCap;
    public const int DefaultGracePeriodDays = 7;
    public const decimal DefaultPricePerEmployee = 49m;
    public const string DefaultPlanName = "Basic";
    public const string CurrencyCode = "INR";
}
