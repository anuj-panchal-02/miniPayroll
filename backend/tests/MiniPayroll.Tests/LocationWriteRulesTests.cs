using MiniPayroll.Domain.Auth;

namespace MiniPayroll.Tests;

public class LocationWriteRulesTests
{
    [Fact]
    public void Names_trim_and_reject_blank_or_overlong_values()
    {
        Assert.Equal("Pune", LocationWriteRules.NormalizeName("  Pune "));
        Assert.True(LocationWriteRules.IsValidName("Pune"));
        Assert.False(LocationWriteRules.IsValidName("  "));
        Assert.False(LocationWriteRules.IsValidName(new string('c', LocationWriteRules.NameMaxLength + 1)));
    }

    [Fact]
    public void Codes_normalize_to_uppercase_letters()
    {
        Assert.Equal("MH", LocationWriteRules.NormalizeCode(" mh "));
        Assert.True(LocationWriteRules.IsValidCode("MH"));
        Assert.True(LocationWriteRules.IsValidCode("an"));
        Assert.False(LocationWriteRules.IsValidCode("M"));
        Assert.False(LocationWriteRules.IsValidCode("MAHA"));
        Assert.False(LocationWriteRules.IsValidCode("M1"));
    }

    [Fact]
    public void Sibling_uniqueness_is_case_insensitive()
    {
        string[] siblings = ["Pune", "Nagpur"];

        Assert.True(LocationWriteRules.IsUniqueAmong("Mumbai", siblings));
        Assert.False(LocationWriteRules.IsUniqueAmong(" pune ", siblings));
    }
}
