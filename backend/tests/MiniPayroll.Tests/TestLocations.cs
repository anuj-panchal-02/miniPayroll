using MiniPayroll.Domain.Entities;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

internal static class TestLocations
{
    public static async Task SeedPuneMaharashtraAsync(MiniPayrollDbContext db)
    {
        if (db.PlatformStates.Any(state => state.Code == "MH"))
        {
            return;
        }

        var state = new PlatformState
        {
            Id = Guid.NewGuid(),
            Name = "Maharashtra",
            Code = "MH",
            IsActive = true,
            SortOrder = 0
        };
        db.PlatformStates.Add(state);
        db.PlatformCities.Add(new PlatformCity
        {
            Id = Guid.NewGuid(),
            StateId = state.Id,
            Name = "Pune",
            IsActive = true,
            SortOrder = 0
        });
        await db.SaveChangesAsync();
    }
}
