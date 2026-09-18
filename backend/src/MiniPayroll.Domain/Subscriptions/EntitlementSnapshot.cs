namespace MiniPayroll.Domain.Subscriptions;

public sealed record EntitlementSnapshot(
    EmployeeUsage Usage,
    bool CanWrite,
    bool CanRunPayroll,
    IReadOnlyList<string> EnabledFeatures)
{
    public static EntitlementSnapshot None { get; } = new(
        new EmployeeUsage(0, 0, 0, false),
        false,
        false,
        []);
}
