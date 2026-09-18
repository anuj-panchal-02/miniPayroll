using System.Globalization;
using MiniPayroll.Domain.Payroll;

namespace MiniPayroll.Domain.Billing;

public sealed record PriorPeriodHold(bool IsHeld, string? PeriodKey);

public static class PriorPeriodBillingHold
{
    public static PriorPeriodHold Evaluate(
        PayrollPeriod opening,
        DateTimeOffset? activatedAt,
        decimal previousAmountDue,
        decimal recordedPaid,
        bool invoicePaid)
    {
        if (activatedAt is not { } activated || !opening.IsValid)
        {
            return new PriorPeriodHold(false, null);
        }

        var previous = BillingCalculator.Previous(opening);
        if (!previous.IsValid)
        {
            return new PriorPeriodHold(false, null);
        }

        var activationMonth = BillingCalculator.CalendarMonth(activated);
        if (BillingCalculator.Compare(previous, activationMonth) < 0)
        {
            return new PriorPeriodHold(false, null);
        }

        if (previousAmountDue <= 0)
        {
            return new PriorPeriodHold(false, null);
        }

        if (invoicePaid || recordedPaid >= previousAmountDue)
        {
            return new PriorPeriodHold(false, null);
        }

        return new PriorPeriodHold(true, BillingCalculator.FormatPeriod(previous));
    }

    public static string Message(string? periodKey)
    {
        if (!BillingCalculator.TryParsePeriod(periodKey, out var period))
        {
            return "The previous subscription period is unpaid.";
        }

        var month = CultureInfo.InvariantCulture.DateTimeFormat.GetMonthName(period.Month);
        return $"{month} {period.Year} subscription invoice is unpaid.";
    }
}
