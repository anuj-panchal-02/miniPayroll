using MiniPayroll.Domain.Constants;

namespace MiniPayroll.Tests;

public class IndianCityCatalogTests
{
    [Fact]
    public void Catalog_maps_at_least_one_city_to_every_indian_state()
    {
        var stateCodes = IndianStateCatalog.All
            .Select(entry => entry.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.All(
            IndianCityCatalog.All,
            city => Assert.Contains(city.StateCode, stateCodes));
        Assert.Equal(
            stateCodes.Count,
            IndianCityCatalog.All
                .Select(city => city.StateCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());
        Assert.Equal(
            IndianCityCatalog.All.Count,
            IndianCityCatalog.All
                .Select(city => $"{city.StateCode}:{city.Name}")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());
        Assert.Contains(
            IndianCityCatalog.All,
            city => city.StateCode == "MH" && city.Name == "Pune");
        Assert.Contains(
            IndianCityCatalog.All,
            city => city.StateCode == "KA" && city.Name == "Bengaluru");
        Assert.Contains(
            IndianCityCatalog.All,
            city => city.StateCode == "DL" && city.Name == "New Delhi");
    }
}
