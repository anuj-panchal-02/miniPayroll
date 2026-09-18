using MiniPayroll.Domain.Constants;

namespace MiniPayroll.Domain.Subscriptions;

public static class SubscriptionSchemaBackfill
{
    public static readonly DateTimeOffset OpenPriceWindow =
        new(2014, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static string PlanCodeFromName(string name)
    {
        if (string.Equals(name, PlatformLimits.DefaultPlanName, StringComparison.OrdinalIgnoreCase))
        {
            return PlatformLimits.DefaultPlanCode;
        }

        var slug = new string(name
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray());
        return string.IsNullOrWhiteSpace(slug) ? PlatformLimits.DefaultPlanCode : slug;
    }

    public static int MaxActiveEmployeesFromLegacy(int defaultEmployeeLimit) =>
        defaultEmployeeLimit > 0 ? defaultEmployeeLimit : PlatformLimits.DefaultEmployeeLimit;

    public static bool IsActiveFromLegacy(bool isPublic) => isPublic;

    public static DateTimeOffset NextBillingDateFromLegacy(
        DateTimeOffset? dueDate,
        DateTimeOffset? currentPeriodEnd) =>
        dueDate ?? currentPeriodEnd ?? default;

    public static DateTimeOffset CreatedAtFromLegacy(
        DateTimeOffset companyCreatedAt,
        DateTimeOffset? currentPeriodStart) =>
        currentPeriodStart is { } started && started < companyCreatedAt
            ? started
            : companyCreatedAt;
}
