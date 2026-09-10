namespace MiniPayroll.Domain.Auth;

public readonly record struct ActiveLocationPair(string State, string City);

public static class LocationMasterRules
{
    public static bool MatchesActivePair(
        string? state,
        string? city,
        IEnumerable<ActiveLocationPair> locations)
    {
        ArgumentNullException.ThrowIfNull(locations);

        var stateName = state?.Trim() ?? string.Empty;
        var cityName = city?.Trim() ?? string.Empty;
        if (stateName.Length == 0 || cityName.Length == 0)
        {
            return false;
        }

        return locations.Any(location =>
            string.Equals(location.State, stateName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(location.City, cityName, StringComparison.OrdinalIgnoreCase));
    }
}
