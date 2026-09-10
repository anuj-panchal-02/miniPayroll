namespace MiniPayroll.Domain.Payroll;

public static class IndianRupeeWords
{
    public static string ToRupees(decimal amount)
    {
        var rupees = decimal.ToInt64(decimal.Truncate(amount));
        if (rupees < 0)
        {
            return "Minus " + ToRupees(-amount);
        }

        var words = rupees == 0 ? "Zero" : Convert(rupees);
        return $"{words} rupees only";
    }

    private static string Convert(long value)
    {
        if (value < 20)
        {
            return Ones[value];
        }
        if (value < 100)
        {
            var tens = Tens[value / 10];
            return value % 10 == 0 ? tens : $"{tens} {Ones[value % 10]}";
        }
        if (value < 1000)
        {
            return Combine(Ones[value / 100] + " hundred", value % 100, "and ");
        }
        if (value < 100_000)
        {
            return Combine(Convert(value / 1000) + " thousand", value % 1000);
        }
        if (value < 10_000_000)
        {
            return Combine(Convert(value / 100_000) + " lakh", value % 100_000);
        }

        return Combine(Convert(value / 10_000_000) + " crore", value % 10_000_000);
    }

    private static string Combine(string head, long remainder, string connector = "")
    {
        if (remainder == 0)
        {
            return head;
        }
        var tail = Convert(remainder);
        if (remainder < 100 && connector.Length > 0)
        {
            return $"{head} {connector}{tail}";
        }
        return $"{head} {tail}";
    }

    private static readonly string[] Ones =
    [
        "Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine",
        "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen",
        "Seventeen", "Eighteen", "Nineteen"
    ];

    private static readonly string[] Tens =
    [
        "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"
    ];
}
