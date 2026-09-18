using DomainMoney = MiniPayroll.Domain.Payroll.PayrollMoney;

namespace MiniPayroll.Tests;

public class PayrollRoundingTests
{
    [Fact]
    public void Rupees_rounds_half_away_from_zero()
    {
        Assert.Equal(1001m, DomainMoney.Rupees(1000.50m));
        Assert.Equal(1000m, DomainMoney.Rupees(1000.49m));
        Assert.Equal(-1001m, DomainMoney.Rupees(-1000.50m));
    }

    [Fact]
    public void Two_decimals_then_rupees_differs_from_rounding_raw_to_rupees()
    {
        Assert.Equal(1000.50m, DomainMoney.TwoDecimals(1000.499m));
        Assert.Equal(1001m, DomainMoney.Rupees(DomainMoney.TwoDecimals(1000.499m)));
        Assert.Equal(1000m, DomainMoney.Rupees(1000.499m));
    }
}
