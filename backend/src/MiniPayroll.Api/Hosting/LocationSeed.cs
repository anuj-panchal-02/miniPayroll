using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Auth;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Api.Hosting;

public static class LocationSeed
{
    public static async Task EnsureSeededAsync(
        MiniPayrollDbContext db,
        CancellationToken cancellationToken = default)
    {
        await SeedStatesAsync(db, cancellationToken);
        await SeedCitiesAsync(db, cancellationToken);
    }

    private static async Task SeedStatesAsync(
        MiniPayrollDbContext db,
        CancellationToken cancellationToken)
    {
        var existingCodes = await db.PlatformStates
            .Select(state => state.Code)
            .ToListAsync(cancellationToken);
        var missing = IndianStateCatalog.All
            .Where(entry => !existingCodes.Contains(entry.Code, StringComparer.OrdinalIgnoreCase))
            .ToList();
        if (missing.Count == 0)
        {
            return;
        }

        var sortOrder = existingCodes.Count == 0
            ? 0
            : await db.PlatformStates.MaxAsync(state => state.SortOrder, cancellationToken) + 1;

        foreach (var entry in missing)
        {
            db.PlatformStates.Add(new Domain.Entities.PlatformState
            {
                Id = Guid.NewGuid(),
                Name = LocationWriteRules.NormalizeName(entry.Name),
                Code = LocationWriteRules.NormalizeCode(entry.Code),
                IsActive = true,
                SortOrder = sortOrder++
            });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedCitiesAsync(
        MiniPayrollDbContext db,
        CancellationToken cancellationToken)
    {
        var states = await db.PlatformStates.ToListAsync(cancellationToken);
        var stateByCode = states.ToDictionary(
            state => state.Code,
            StringComparer.OrdinalIgnoreCase);
        var existing = await db.PlatformCities
            .Select(city => new { city.StateId, city.Name, city.SortOrder })
            .ToListAsync(cancellationToken);
        var existingKeys = existing
            .Select(city => Key(city.StateId, city.Name))
            .ToHashSet(StringComparer.Ordinal);
        var nextOrder = existing
            .GroupBy(city => city.StateId)
            .ToDictionary(group => group.Key, group => group.Max(city => city.SortOrder) + 1);

        var added = 0;
        foreach (var entry in IndianCityCatalog.All)
        {
            if (!stateByCode.TryGetValue(entry.StateCode, out var state))
            {
                continue;
            }

            var name = LocationWriteRules.NormalizeName(entry.Name);
            if (!existingKeys.Add(Key(state.Id, name)))
            {
                continue;
            }

            var sortOrder = nextOrder.GetValueOrDefault(state.Id, 0);
            db.PlatformCities.Add(new Domain.Entities.PlatformCity
            {
                Id = Guid.NewGuid(),
                StateId = state.Id,
                Name = name,
                IsActive = true,
                SortOrder = sortOrder
            });
            nextOrder[state.Id] = sortOrder + 1;
            added++;
        }

        if (added > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static string Key(Guid stateId, string name) =>
        $"{stateId:N}:{LocationWriteRules.NormalizeName(name).ToUpperInvariant()}";
}
