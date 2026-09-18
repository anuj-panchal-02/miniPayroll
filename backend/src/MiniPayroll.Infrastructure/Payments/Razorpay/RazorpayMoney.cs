namespace MiniPayroll.Infrastructure.Payments.Razorpay;

public static class RazorpayMoney
{
    public static int ToPaise(decimal amount) =>
        (int)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);

    public static decimal FromPaise(int paise) => paise / 100m;

    public static bool SameAmount(decimal rupees, int paise) =>
        ToPaise(rupees) == paise;
}
