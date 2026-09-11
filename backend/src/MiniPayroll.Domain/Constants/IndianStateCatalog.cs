namespace MiniPayroll.Domain.Constants;

public readonly record struct IndianStateEntry(string Name, string Code);

public static class IndianStateCatalog
{
    public static readonly IReadOnlyList<IndianStateEntry> All =
    [
        new("Andhra Pradesh", "AP"),
        new("Arunachal Pradesh", "AR"),
        new("Assam", "AS"),
        new("Bihar", "BR"),
        new("Chhattisgarh", "CG"),
        new("Goa", "GA"),
        new("Gujarat", "GJ"),
        new("Haryana", "HR"),
        new("Himachal Pradesh", "HP"),
        new("Jharkhand", "JH"),
        new("Karnataka", "KA"),
        new("Kerala", "KL"),
        new("Madhya Pradesh", "MP"),
        new("Maharashtra", "MH"),
        new("Manipur", "MN"),
        new("Meghalaya", "ML"),
        new("Mizoram", "MZ"),
        new("Nagaland", "NL"),
        new("Odisha", "OD"),
        new("Punjab", "PB"),
        new("Rajasthan", "RJ"),
        new("Sikkim", "SK"),
        new("Tamil Nadu", "TN"),
        new("Telangana", "TS"),
        new("Tripura", "TR"),
        new("Uttar Pradesh", "UP"),
        new("Uttarakhand", "UK"),
        new("West Bengal", "WB"),
        new("Andaman and Nicobar Islands", "AN"),
        new("Chandigarh", "CH"),
        new("Dadra and Nagar Haveli and Daman and Diu", "DH"),
        new("Delhi", "DL"),
        new("Jammu and Kashmir", "JK"),
        new("Ladakh", "LA"),
        new("Lakshadweep", "LD"),
        new("Puducherry", "PY")
    ];

    public static string? CodeFor(string? nameOrCode)
    {
        var value = nameOrCode?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        var match = All.FirstOrDefault(entry =>
            entry.Name.Equals(value, StringComparison.OrdinalIgnoreCase)
            || entry.Code.Equals(value, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrEmpty(match.Code) ? null : match.Code;
    }
}
