using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Auth;
using MiniPayroll.Domain.Entities;

namespace MiniPayroll.Infrastructure.Persistence;

public enum LocationCatalogStatus
{
    Success,
    NotFound,
    InvalidInput,
    Duplicate
}

public sealed record PlatformStateItem(
    Guid Id,
    string Name,
    string Code,
    bool IsActive,
    int SortOrder);

public sealed record PlatformCityItem(
    Guid Id,
    Guid StateId,
    string Name,
    bool IsActive,
    int SortOrder);

public sealed record LocationCatalogResult<T>(
    LocationCatalogStatus Status,
    T? Value = default);

public static class LocationCatalogLookups
{
    public static async Task<bool> HasActivePairAsync(
        MiniPayrollDbContext db,
        string? state,
        string? city,
        CancellationToken cancellationToken = default)
    {
        var pairs = await db.PlatformCities
            .AsNoTracking()
            .Where(item => item.IsActive && item.State.IsActive)
            .Select(item => new ActiveLocationPair(item.State.Name, item.Name))
            .ToListAsync(cancellationToken);

        return LocationMasterRules.MatchesActivePair(state, city, pairs);
    }
}

public sealed class LocationCatalogService(MiniPayrollDbContext db)
{
    public async Task<IReadOnlyList<PlatformStateItem>> ListStatesAsync(
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        var query = db.PlatformStates.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(state => state.IsActive);
        }

        return await query
            .OrderBy(state => state.SortOrder)
            .ThenBy(state => state.Name)
            .Select(state => new PlatformStateItem(
                state.Id,
                state.Name,
                state.Code,
                state.IsActive,
                state.SortOrder))
            .ToListAsync(cancellationToken);
    }

    public async Task<LocationCatalogResult<IReadOnlyList<PlatformCityItem>>> ListCitiesAsync(
        Guid stateId,
        bool includeInactive,
        CancellationToken cancellationToken = default)
    {
        var stateExists = await db.PlatformStates
            .AsNoTracking()
            .AnyAsync(state => state.Id == stateId, cancellationToken);
        if (!stateExists)
        {
            return new LocationCatalogResult<IReadOnlyList<PlatformCityItem>>(
                LocationCatalogStatus.NotFound);
        }

        var query = db.PlatformCities.AsNoTracking().Where(city => city.StateId == stateId);
        if (!includeInactive)
        {
            query = query.Where(city => city.IsActive);
        }

        var cities = await query
            .OrderBy(city => city.SortOrder)
            .ThenBy(city => city.Name)
            .Select(city => new PlatformCityItem(
                city.Id,
                city.StateId,
                city.Name,
                city.IsActive,
                city.SortOrder))
            .ToListAsync(cancellationToken);

        return new LocationCatalogResult<IReadOnlyList<PlatformCityItem>>(
            LocationCatalogStatus.Success,
            cities);
    }

    public async Task<LocationCatalogResult<PlatformStateItem>> CreateStateAsync(
        string? name,
        string? code,
        CancellationToken cancellationToken = default)
    {
        var normalizedName = LocationWriteRules.NormalizeName(name);
        var normalizedCode = LocationWriteRules.NormalizeCode(code);
        if (!LocationWriteRules.IsValidName(normalizedName)
            || !LocationWriteRules.IsValidCode(normalizedCode))
        {
            return new LocationCatalogResult<PlatformStateItem>(LocationCatalogStatus.InvalidInput);
        }

        var existing = await db.PlatformStates
            .Select(state => new { state.Name, state.Code })
            .ToListAsync(cancellationToken);
        if (!LocationWriteRules.IsUniqueAmong(normalizedName, existing.Select(state => state.Name))
            || !LocationWriteRules.IsUniqueAmong(normalizedCode, existing.Select(state => state.Code)))
        {
            return new LocationCatalogResult<PlatformStateItem>(LocationCatalogStatus.Duplicate);
        }

        var maxOrder = existing.Count == 0
            ? -1
            : await db.PlatformStates.MaxAsync(state => state.SortOrder, cancellationToken);
        var state = new PlatformState
        {
            Id = Guid.NewGuid(),
            Name = normalizedName,
            Code = normalizedCode,
            IsActive = true,
            SortOrder = maxOrder + 1
        };
        db.PlatformStates.Add(state);
        await db.SaveChangesAsync(cancellationToken);
        return new LocationCatalogResult<PlatformStateItem>(
            LocationCatalogStatus.Success,
            ToStateItem(state));
    }

    public async Task<LocationCatalogResult<PlatformStateItem>> UpdateStateAsync(
        Guid id,
        string? name,
        string? code,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var state = await db.PlatformStates.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (state is null)
        {
            return new LocationCatalogResult<PlatformStateItem>(LocationCatalogStatus.NotFound);
        }

        var nextName = name is null ? state.Name : LocationWriteRules.NormalizeName(name);
        var nextCode = code is null ? state.Code : LocationWriteRules.NormalizeCode(code);
        if (!LocationWriteRules.IsValidName(nextName) || !LocationWriteRules.IsValidCode(nextCode))
        {
            return new LocationCatalogResult<PlatformStateItem>(LocationCatalogStatus.InvalidInput);
        }

        var siblings = await db.PlatformStates
            .Where(item => item.Id != id)
            .Select(item => new { item.Name, item.Code })
            .ToListAsync(cancellationToken);
        if (!LocationWriteRules.IsUniqueAmong(nextName, siblings.Select(item => item.Name))
            || !LocationWriteRules.IsUniqueAmong(nextCode, siblings.Select(item => item.Code)))
        {
            return new LocationCatalogResult<PlatformStateItem>(LocationCatalogStatus.Duplicate);
        }

        state.Name = nextName;
        state.Code = nextCode;
        if (isActive is { } active)
        {
            state.IsActive = active;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new LocationCatalogResult<PlatformStateItem>(
            LocationCatalogStatus.Success,
            ToStateItem(state));
    }

    public async Task<LocationCatalogResult<PlatformCityItem>> CreateCityAsync(
        Guid stateId,
        string? name,
        CancellationToken cancellationToken = default)
    {
        var stateExists = await db.PlatformStates.AnyAsync(state => state.Id == stateId, cancellationToken);
        if (!stateExists)
        {
            return new LocationCatalogResult<PlatformCityItem>(LocationCatalogStatus.NotFound);
        }

        var normalizedName = LocationWriteRules.NormalizeName(name);
        if (!LocationWriteRules.IsValidName(normalizedName))
        {
            return new LocationCatalogResult<PlatformCityItem>(LocationCatalogStatus.InvalidInput);
        }

        var siblings = await db.PlatformCities
            .Where(city => city.StateId == stateId)
            .Select(city => city.Name)
            .ToListAsync(cancellationToken);
        if (!LocationWriteRules.IsUniqueAmong(normalizedName, siblings))
        {
            return new LocationCatalogResult<PlatformCityItem>(LocationCatalogStatus.Duplicate);
        }

        var maxOrder = siblings.Count == 0
            ? -1
            : await db.PlatformCities
                .Where(city => city.StateId == stateId)
                .MaxAsync(city => city.SortOrder, cancellationToken);
        var city = new PlatformCity
        {
            Id = Guid.NewGuid(),
            StateId = stateId,
            Name = normalizedName,
            IsActive = true,
            SortOrder = maxOrder + 1
        };
        db.PlatformCities.Add(city);
        await db.SaveChangesAsync(cancellationToken);
        return new LocationCatalogResult<PlatformCityItem>(
            LocationCatalogStatus.Success,
            ToCityItem(city));
    }

    public async Task<LocationCatalogResult<PlatformCityItem>> UpdateCityAsync(
        Guid id,
        string? name,
        bool? isActive,
        CancellationToken cancellationToken = default)
    {
        var city = await db.PlatformCities.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (city is null)
        {
            return new LocationCatalogResult<PlatformCityItem>(LocationCatalogStatus.NotFound);
        }

        var nextName = name is null ? city.Name : LocationWriteRules.NormalizeName(name);
        if (!LocationWriteRules.IsValidName(nextName))
        {
            return new LocationCatalogResult<PlatformCityItem>(LocationCatalogStatus.InvalidInput);
        }

        var siblings = await db.PlatformCities
            .Where(item => item.StateId == city.StateId && item.Id != id)
            .Select(item => item.Name)
            .ToListAsync(cancellationToken);
        if (!LocationWriteRules.IsUniqueAmong(nextName, siblings))
        {
            return new LocationCatalogResult<PlatformCityItem>(LocationCatalogStatus.Duplicate);
        }

        city.Name = nextName;
        if (isActive is { } active)
        {
            city.IsActive = active;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new LocationCatalogResult<PlatformCityItem>(
            LocationCatalogStatus.Success,
            ToCityItem(city));
    }

    private static PlatformStateItem ToStateItem(PlatformState state) =>
        new(state.Id, state.Name, state.Code, state.IsActive, state.SortOrder);

    private static PlatformCityItem ToCityItem(PlatformCity city) =>
        new(city.Id, city.StateId, city.Name, city.IsActive, city.SortOrder);
}
