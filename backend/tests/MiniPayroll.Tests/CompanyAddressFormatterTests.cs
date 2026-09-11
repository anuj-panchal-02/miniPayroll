using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Payroll;

namespace MiniPayroll.Tests;

public class CompanyAddressFormatterTests
{
    [Fact]
    public void Joins_non_empty_parts_like_setup_review()
    {
        var company = new Company
        {
            AddressLine1 = "1 Main Street",
            AddressLine2 = "Suite 4",
            City = "Pune",
            State = "Maharashtra",
            PostalCode = "411001"
        };

        Assert.Equal("1 Main Street, Suite 4, Pune, Maharashtra, 411001", CompanyAddressFormatter.Format(company));
    }

    [Fact]
    public void Skips_blank_parts_and_returns_null_when_empty()
    {
        Assert.Equal("Pune, Maharashtra", CompanyAddressFormatter.Format(new Company
        {
            AddressLine1 = "  ",
            City = "Pune",
            State = "Maharashtra"
        }));
        Assert.Null(CompanyAddressFormatter.Format(new Company()));
    }
}
