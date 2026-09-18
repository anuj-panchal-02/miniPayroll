using MiniPayroll.Domain.Auth;
using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Tests;

public class SubscriptionMutationRulesTests
{
    [Theory]
    [InlineData(SubscriptionStatus.Active, true)]
    [InlineData(SubscriptionStatus.PastDue, true)]
    [InlineData(SubscriptionStatus.GracePeriod, true)]
    [InlineData(SubscriptionStatus.Trialing, false)]
    [InlineData(SubscriptionStatus.Suspended, false)]
    [InlineData(SubscriptionStatus.Cancelled, false)]
    [InlineData(SubscriptionStatus.Expired, false)]
    public void Writes_are_allowed_only_for_active_and_past_due(
        SubscriptionStatus status,
        bool expected)
    {
        Assert.Equal(expected, SubscriptionMutationRules.CanMutate(status));
    }

    [Fact]
    public void Missing_subscription_cannot_mutate()
    {
        Assert.False(SubscriptionMutationRules.CanMutate(null));
    }

    [Fact]
    public void Open_trial_can_write_and_dateless_trial_cannot()
    {
        var now = new DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);
        Assert.True(SubscriptionMutationRules.CanMutate(SubscriptionStatus.Trialing, now.AddDays(3), now));
        Assert.False(SubscriptionMutationRules.CanMutate(SubscriptionStatus.Trialing, null, now));
        Assert.False(SubscriptionMutationRules.CanMutate(SubscriptionStatus.Trialing, now.AddHours(-1), now));
        Assert.False(SubscriptionMutationRules.CanMutate(SubscriptionStatus.Suspended, now.AddDays(3), now));
    }
}
