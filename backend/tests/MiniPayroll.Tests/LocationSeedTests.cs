using MiniPayroll.Api.Hosting;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Tenancy;

namespace MiniPayroll.Tests;

public class LocationSeedTests
{
    [Fact]
    public async Task Seed_inserts_the_full_indian_catalog_once()
    {
        await using var db = TestDb.Create(NullTenantContext.Instance, $"seed-{Guid.NewGuid():N}");

        await LocationSeed.EnsureSeededAsync(db);
        await LocationSeed.EnsureSeededAsync(db);

        Assert.Equal(IndianStateCatalog.All.Count, db.PlatformStates.Count());
        Assert.Equal(IndianCityCatalog.All.Count, db.PlatformCities.Count());
        Assert.Contains(db.PlatformStates, state => state.Code == "MH" && state.Name == "Maharashtra");
        var maharashtra = db.PlatformStates.Single(state => state.Code == "MH");
        Assert.Contains(
            db.PlatformCities,
            city => city.StateId == maharashtra.Id && city.Name == "Pune");
        Assert.Contains(
            db.PlatformCities,
            city => city.StateId == maharashtra.Id && city.Name == "Mumbai");
    }
}
