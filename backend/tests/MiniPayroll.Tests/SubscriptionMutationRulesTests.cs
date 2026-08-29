using MiniPayroll.Domain.Auth;
using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Tests;

public class SubscriptionMutationRulesTests
{
    [Theory]
    [InlineData(SubscriptionStatus.Active, true)]
    [InlineData(SubscriptionStatus.PastDue, true)]
    [InlineData(SubscriptionStatus.Pending, false)]
    [InlineData(SubscriptionStatus.Suspended, false)]
    [InlineData(SubscriptionStatus.Cancelled, false)]
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
}
