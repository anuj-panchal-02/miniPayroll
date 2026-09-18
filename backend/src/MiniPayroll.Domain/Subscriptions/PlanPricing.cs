using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Subscriptions;

public static class PlanPricing
{
    public static PlanPrice? Current(
        IEnumerable<PlanPrice> prices,
        BillingCycle cycle,
        DateTimeOffset on)
    {
        return prices
            .Where(price =>
                price.BillingCycle == cycle
                && price.EffectiveFrom <= on
                && (price.EffectiveTo is null || price.EffectiveTo >= on))
            .OrderByDescending(price => price.EffectiveFrom)
            .ThenByDescending(price => price.IsActive)
            .FirstOrDefault();
    }

    public static decimal AmountOrFallback(
        IEnumerable<PlanPrice> prices,
        BillingCycle cycle,
        DateTimeOffset on,
        decimal fallback)
    {
        return Current(prices, cycle, on)?.Amount ?? fallback;
    }

    public static PlanPrice Revise(
        Plan plan,
        BillingCycle cycle,
        decimal amount,
        DateTimeOffset now,
        string currency = PlatformLimits.CurrencyCode)
    {
        foreach (var open in plan.Prices.Where(price =>
                     price.BillingCycle == cycle
                     && price.IsActive
                     && price.EffectiveTo is null))
        {
            open.EffectiveTo = now;
            open.IsActive = false;
        }

        var next = new PlanPrice
        {
            Id = Guid.NewGuid(),
            PlanId = plan.Id,
            BillingCycle = cycle,
            Amount = amount,
            Currency = currency,
            EffectiveFrom = now,
            EffectiveTo = null,
            IsActive = true
        };
        plan.Prices.Add(next);

        if (cycle == BillingCycle.Monthly)
        {
            plan.PricePerEmployee = amount;
        }

        return next;
    }
}
