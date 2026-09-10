using MiniPayroll.Domain.Payroll;

namespace MiniPayroll.Tests;

public sealed class IndianRupeeWordsTests
{
    [Theory]
    [InlineData(0, "Zero rupees only")]
    [InlineData(1, "One rupees only")]
    [InlineData(21, "Twenty One rupees only")]
    [InlineData(28000, "Twenty Eight thousand rupees only")]
    [InlineData(125000, "One lakh Twenty Five thousand rupees only")]
    public void Formats_indian_numbering(decimal amount, string expected)
    {
        Assert.Equal(expected, IndianRupeeWords.ToRupees(amount));
    }
}
