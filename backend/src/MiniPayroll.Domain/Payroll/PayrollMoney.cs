namespace MiniPayroll.Domain.Payroll;

public static class PayrollMoney
{
    public static decimal Rupees(decimal amount) =>
        decimal.Round(amount, 0, MidpointRounding.AwayFromZero);

    public static decimal TwoDecimals(decimal amount) =>
        decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}
