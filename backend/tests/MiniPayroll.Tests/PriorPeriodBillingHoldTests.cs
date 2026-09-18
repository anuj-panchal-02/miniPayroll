using MiniPayroll.Domain.Billing;
using MiniPayroll.Domain.Payroll;

namespace MiniPayroll.Tests;

public class PriorPeriodBillingHoldTests
{
    private static readonly DateTimeOffset ActivatedAugust =
        new(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);

    private static readonly PayrollPeriod September = new(2026, 9);
    private static readonly PayrollPeriod August = new(2026, 8);

    [Fact]
    public void First_month_is_not_held()
    {
        var hold = PriorPeriodBillingHold.Evaluate(
            August, ActivatedAugust, previousAmountDue: 98m, recordedPaid: 0m, invoicePaid: false);

        Assert.False(hold.IsHeld);
        Assert.Null(hold.PeriodKey);
    }

    [Fact]
    public void Previous_month_before_activation_is_not_held()
    {
        var hold = PriorPeriodBillingHold.Evaluate(
            August,
            new DateTimeOffset(2026, 8, 15, 0, 0, 0, TimeSpan.Zero),
            previousAmountDue: 98m,
            recordedPaid: 0m,
            invoicePaid: false);

        Assert.False(hold.IsHeld);
    }

    [Fact]
    public void Missing_activation_is_not_held()
    {
        var hold = PriorPeriodBillingHold.Evaluate(
            September, null, previousAmountDue: 98m, recordedPaid: 0m, invoicePaid: false);

        Assert.False(hold.IsHeld);
    }

    [Fact]
    public void Zero_amount_due_is_not_held()
    {
        var hold = PriorPeriodBillingHold.Evaluate(
            September, ActivatedAugust, previousAmountDue: 0m, recordedPaid: 0m, invoicePaid: false);

        Assert.False(hold.IsHeld);
        Assert.Null(hold.PeriodKey);
    }

    [Fact]
    public void Unpaid_previous_period_is_held()
    {
        var hold = PriorPeriodBillingHold.Evaluate(
            September, ActivatedAugust, previousAmountDue: 98m, recordedPaid: 0m, invoicePaid: false);

        Assert.True(hold.IsHeld);
        Assert.Equal("2026-08", hold.PeriodKey);
        Assert.Equal("August 2026 subscription invoice is unpaid.", PriorPeriodBillingHold.Message(hold.PeriodKey));
    }

    [Fact]
    public void Partial_payment_is_still_held()
    {
        var hold = PriorPeriodBillingHold.Evaluate(
            September, ActivatedAugust, previousAmountDue: 98m, recordedPaid: 40m, invoicePaid: false);

        Assert.True(hold.IsHeld);
        Assert.Equal("2026-08", hold.PeriodKey);
    }

    [Fact]
    public void Payment_rows_without_invoice_settle_the_period()
    {
        var hold = PriorPeriodBillingHold.Evaluate(
            September, ActivatedAugust, previousAmountDue: 98m, recordedPaid: 98m, invoicePaid: false);

        Assert.False(hold.IsHeld);
    }

    [Fact]
    public void Invoice_paid_without_payment_rows_settles_the_period()
    {
        var hold = PriorPeriodBillingHold.Evaluate(
            September, ActivatedAugust, previousAmountDue: 98m, recordedPaid: 0m, invoicePaid: true);

        Assert.False(hold.IsHeld);
    }

    [Fact]
    public void Overpayment_settles_the_period()
    {
        var hold = PriorPeriodBillingHold.Evaluate(
            September, ActivatedAugust, previousAmountDue: 98m, recordedPaid: 120m, invoicePaid: false);

        Assert.False(hold.IsHeld);
    }

    [Fact]
    public void January_looks_back_to_december()
    {
        var hold = PriorPeriodBillingHold.Evaluate(
            new PayrollPeriod(2027, 1),
            new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.Zero),
            previousAmountDue: 49m,
            recordedPaid: 0m,
            invoicePaid: false);

        Assert.True(hold.IsHeld);
        Assert.Equal("2026-12", hold.PeriodKey);
        Assert.Equal("December 2026 subscription invoice is unpaid.", PriorPeriodBillingHold.Message(hold.PeriodKey));
    }
}

public class BillingCalculatorPreviousTests
{
    [Fact]
    public void Previous_month_stays_in_the_same_year()
    {
        Assert.Equal(new PayrollPeriod(2026, 8), BillingCalculator.Previous(new PayrollPeriod(2026, 9)));
    }

    [Fact]
    public void Previous_january_is_december()
    {
        Assert.Equal(new PayrollPeriod(2026, 12), BillingCalculator.Previous(new PayrollPeriod(2027, 1)));
    }
}
