using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public class LocationCatalogServiceTests
{
    [Fact]
    public async Task Default_lists_hide_inactive_locations()
    {
        var (db, service, stateId) = await CreateAsync();
        await using var owned = db;
        var city = await service.CreateCityAsync(stateId, "Pune");
        Assert.Equal(LocationCatalogStatus.Success, city.Status);
        await service.UpdateCityAsync(city.Value!.Id, null, false);
        await service.UpdateStateAsync(stateId, null, null, false);

        var states = await service.ListStatesAsync(includeInactive: false);
        var cities = await service.ListCitiesAsync(stateId, includeInactive: false);

        Assert.DoesNotContain(states, state => state.Id == stateId);
        Assert.Equal(LocationCatalogStatus.Success, cities.Status);
        Assert.Empty(cities.Value!);

        var allStates = await service.ListStatesAsync(includeInactive: true);
        var allCities = await service.ListCitiesAsync(stateId, includeInactive: true);
        Assert.Contains(allStates, state => state.Id == stateId && !state.IsActive);
        Assert.Contains(allCities.Value!, item => item.Name == "Pune" && !item.IsActive);
    }

    [Fact]
    public async Task Duplicate_names_are_rejected_case_insensitively()
    {
        var (db, service, stateId) = await CreateAsync();
        await using var owned = db;

        Assert.Equal(LocationCatalogStatus.Duplicate, (await service.CreateStateAsync("karnataka", "KA")).Status);
        Assert.Equal(LocationCatalogStatus.Success, (await service.CreateCityAsync(stateId, "Pune")).Status);
        Assert.Equal(LocationCatalogStatus.Duplicate, (await service.CreateCityAsync(stateId, " pune ")).Status);
    }

    [Fact]
    public async Task Active_pair_lookup_requires_an_active_city_in_an_active_state()
    {
        var (db, service, stateId) = await CreateAsync();
        await using var owned = db;
        await service.CreateCityAsync(stateId, "Pune");

        Assert.True(await LocationCatalogLookups.HasActivePairAsync(db, "Karnataka", "Pune"));

        var city = (await service.ListCitiesAsync(stateId, true)).Value!.Single();
        await service.UpdateCityAsync(city.Id, null, false);
        Assert.False(await LocationCatalogLookups.HasActivePairAsync(db, "Karnataka", "Pune"));
    }

    [Fact]
    public async Task City_create_requires_an_existing_state()
    {
        await using var db = TestDb.Create(NullTenantContext.Instance, UniqueDatabase());
        var service = new LocationCatalogService(db);

        var result = await service.CreateCityAsync(Guid.NewGuid(), "Pune");

        Assert.Equal(LocationCatalogStatus.NotFound, result.Status);
    }

    private static async Task<(MiniPayrollDbContext Db, LocationCatalogService Service, Guid StateId)>
        CreateAsync()
    {
        var db = TestDb.Create(NullTenantContext.Instance, UniqueDatabase());
        var service = new LocationCatalogService(db);
        var state = await service.CreateStateAsync("Karnataka", "KA");
        return (db, service, state.Value!.Id);
    }

    private static string UniqueDatabase() => $"locations-{Guid.NewGuid():N}";
}
