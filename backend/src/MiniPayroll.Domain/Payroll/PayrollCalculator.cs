namespace MiniPayroll.Domain.Payroll;

public static class PayrollCalculator
{
    public const string BasicSalaryName = "Basic Salary";
    public const decimal UnpaidLeaveWarningThreshold = 5m;
    public const decimal NetChangeWarningRatio = 0.20m;

    public static PayrollEmployeeResult Calculate(PayrollEmployeeInput input) =>
        PayrollCalculationEngine.Calculate(input);
}
