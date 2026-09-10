using MiniPayroll.Domain.Payroll;

namespace MiniPayroll.Domain.Billing;

public enum BillableSource
{
    FinalizedPayroll = 0,
    ActiveHeadcount = 1
}

public readonly record struct BillableCount(int Count, BillableSource Source);

public static class BillingCalculator
{
    public const string ModeUpi = "UPI";
    public const string ModeNeft = "NEFT";
    public const string ModeCash = "Cash";

    public static readonly IReadOnlyList<string> AllowedModes = [ModeUpi, ModeNeft, ModeCash];

    public static bool TryParsePeriod(string? value, out PayrollPeriod period)
    {
        period = default;
        if (value is not { Length: 7 } || value[4] != '-')
        {
            return false;
        }

        if (!int.TryParse(value.AsSpan(0, 4), out var year)
            || !int.TryParse(value.AsSpan(5, 2), out var month))
        {
            return false;
        }

        var parsed = new PayrollPeriod(year, month);
        if (!parsed.IsValid)
        {
            return false;
        }

        period = parsed;
        return true;
    }

    public static string FormatPeriod(PayrollPeriod period) =>
        $"{period.Year}-{period.Month:D2}";

    public static BillableCount BillableEmployees(
        bool hasFinalizedRun,
        int finalizedDistinctEmployees,
        int eligibleHeadcount) =>
        hasFinalizedRun
            ? new BillableCount(finalizedDistinctEmployees, BillableSource.FinalizedPayroll)
            : new BillableCount(eligibleHeadcount, BillableSource.ActiveHeadcount);

    public static decimal AmountDue(
        decimal pricePerEmployee,
        int billableEmployees,
        PayrollPeriod period,
        DateTimeOffset? activatedAt)
    {
        var full = pricePerEmployee * billableEmployees;
        if (!TryProrationFactor(period, activatedAt, out var factor))
        {
            return Round(full);
        }

        return Round(full * factor);
    }

    public static bool IsProrated(PayrollPeriod period, DateTimeOffset? activatedAt) =>
        TryProrationFactor(period, activatedAt, out var factor) && factor < 1m;

    public static PayrollPeriod CalendarMonth(DateTimeOffset utcNow) =>
        new(utcNow.UtcDateTime.Year, utcNow.UtcDateTime.Month);

    public static bool IsCurrent(PayrollPeriod period, DateTimeOffset utcNow) =>
        Compare(period, CalendarMonth(utcNow)) == 0;

    public static PayrollPeriod BillingEnd(
        DateTimeOffset utcNow,
        IEnumerable<PayrollPeriod> finalizedPeriods)
    {
        var end = CalendarMonth(utcNow);
        foreach (var period in finalizedPeriods)
        {
            if (period.IsValid && Compare(period, end) > 0)
            {
                end = period;
            }
        }

        return end;
    }

    public static IReadOnlyList<PayrollPeriod> PeriodsThrough(
        DateTimeOffset activatedAt,
        DateTimeOffset utcNow) =>
        PeriodsThrough(activatedAt, CalendarMonth(utcNow));

    public static IReadOnlyList<PayrollPeriod> PeriodsThrough(
        DateTimeOffset activatedAt,
        PayrollPeriod end)
    {
        var start = new PayrollPeriod(activatedAt.UtcDateTime.Year, activatedAt.UtcDateTime.Month);
        if (!start.IsValid || !end.IsValid || Compare(start, end) > 0)
        {
            return [];
        }

        var periods = new List<PayrollPeriod>();
        var current = start;
        while (Compare(current, end) <= 0)
        {
            periods.Add(current);
            current = current.Month == 12
                ? new PayrollPeriod(current.Year + 1, 1)
                : new PayrollPeriod(current.Year, current.Month + 1);
        }

        return periods;
    }

    public static bool IsAllowedMode(string? mode) =>
        mode is not null && AllowedModes.Contains(mode, StringComparer.Ordinal);

    public static bool IsValidAmount(decimal amount) =>
        amount > 0 && decimal.Round(amount, 2, MidpointRounding.AwayFromZero) == amount;

    public static bool IsClosed(PayrollPeriod period, DateTimeOffset utcNow)
    {
        var current = new PayrollPeriod(utcNow.UtcDateTime.Year, utcNow.UtcDateTime.Month);
        return Compare(period, current) < 0;
    }

    public static DateTimeOffset DueDate(PayrollPeriod period) =>
        new(period.LastDay.Year, period.LastDay.Month, period.LastDay.Day, 23, 59, 59, TimeSpan.Zero);

    public static bool IsOverdue(decimal remaining, DateTimeOffset dueDate, DateTimeOffset utcNow) =>
        remaining > 0 && utcNow > dueDate;

    public static bool IsPastGrace(
        decimal remaining,
        DateTimeOffset dueDate,
        int gracePeriodDays,
        DateTimeOffset utcNow) =>
        remaining > 0 && utcNow > dueDate.AddDays(Math.Max(gracePeriodDays, 0));

    private static bool TryProrationFactor(
        PayrollPeriod period,
        DateTimeOffset? activatedAt,
        out decimal factor)
    {
        factor = 1m;
        if (activatedAt is not { } activated)
        {
            return false;
        }

        var activationDate = DateOnly.FromDateTime(activated.UtcDateTime);
        if (activationDate < period.FirstDay || activationDate > period.LastDay)
        {
            return false;
        }

        var remainingDays = period.LastDay.DayNumber - activationDate.DayNumber + 1;
        factor = remainingDays / (decimal)period.CalendarDays;
        return true;
    }

    private static int Compare(PayrollPeriod left, PayrollPeriod right) =>
        left.Year != right.Year ? left.Year.CompareTo(right.Year) : left.Month.CompareTo(right.Month);

    private static decimal Round(decimal amount) =>
        decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
}
