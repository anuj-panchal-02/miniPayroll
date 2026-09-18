using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Subscriptions;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Payments;

namespace MiniPayroll.Infrastructure.Persistence;

public sealed class SubscriptionClock(
    MiniPayrollDbContext db,
    TimeProvider? time = null,
    PaymentGatewayService? gateway = null)
{
    public async Task<int> TickAsync(CancellationToken cancellationToken = default)
    {
        var now = (time ?? TimeProvider.System).GetUtcNow();
        var subscriptions = await db.Subscriptions
            .IgnoreQueryFilters()
            .ToListAsync(cancellationToken);

        var lifecycle = new SubscriptionLifecycleService(
            db,
            NullTenantContext.Instance,
            time ?? TimeProvider.System,
            gateway);

        var applied = 0;
        foreach (var subscription in subscriptions)
        {
            if (SubscriptionClockRules.DueCommand(subscription, now) is not { } command)
            {
                continue;
            }

            var result = await lifecycle.ExecuteAsync(
                subscription.CompanyId,
                command,
                Guid.Empty,
                requireAdmin: false,
                cancellationToken);
            if (result.Status == SubscriptionCommandStatus.Success)
            {
                applied++;
            }
        }

        return applied;
    }
}
