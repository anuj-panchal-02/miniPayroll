namespace MiniPayroll.Domain.Billing;

public static class InvoiceTotals
{
    public static decimal Round(decimal amount) =>
        decimal.Round(amount, 2, MidpointRounding.AwayFromZero);

    public static decimal LineAmount(decimal quantity, decimal unitPrice) =>
        Round(quantity * unitPrice);

    public static decimal Subtotal(IEnumerable<decimal> lineAmounts) =>
        Round(lineAmounts.Sum());

    public static decimal Total(decimal subtotal, decimal tax) =>
        Round(subtotal + tax);

    public static bool IsValidMoney(decimal amount) =>
        amount >= 0 && Round(amount) == amount;
}
