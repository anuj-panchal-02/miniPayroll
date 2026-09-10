using MiniPayroll.Domain.Constants;

namespace MiniPayroll.Tests;

public class IndianStateCatalogTests
{
    [Fact]
    public void Catalog_has_thirty_six_unique_states_and_codes()
    {
        Assert.Equal(36, IndianStateCatalog.All.Count);
        Assert.Equal(36, IndianStateCatalog.All.Select(entry => entry.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Equal(36, IndianStateCatalog.All.Select(entry => entry.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(IndianStateCatalog.All, entry => entry.Name == "Maharashtra" && entry.Code == "MH");
        Assert.Contains(IndianStateCatalog.All, entry => entry.Name == "Delhi" && entry.Code == "DL");
    }
}
