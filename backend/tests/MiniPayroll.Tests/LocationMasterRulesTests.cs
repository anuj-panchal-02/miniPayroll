using MiniPayroll.Domain.Auth;

namespace MiniPayroll.Tests;

public class LocationMasterRulesTests
{
    private static readonly ActiveLocationPair[] PuneMaharashtra =
    [
        new("Maharashtra", "Pune")
    ];

    [Fact]
    public void Active_pair_matches_trimmed_case_insensitive_names()
    {
        Assert.True(LocationMasterRules.MatchesActivePair("  maharashtra ", " pune", PuneMaharashtra));
    }

    [Theory]
    [InlineData("Maharashtra", "Mumbai")]
    [InlineData("Karnataka", "Pune")]
    [InlineData("", "Pune")]
    [InlineData("Maharashtra", "")]
    [InlineData(null, "Pune")]
    public void Unknown_or_incomplete_pairs_are_rejected(string? state, string? city)
    {
        Assert.False(LocationMasterRules.MatchesActivePair(state, city, PuneMaharashtra));
    }

    [Fact]
    public void Inactive_catalog_has_no_matches()
    {
        Assert.False(LocationMasterRules.MatchesActivePair("Maharashtra", "Pune", []));
    }
}
