using MiniPayroll.Domain.Auth;
using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Tests;

public class CompanyAdminLoginAccessTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData(SubscriptionStatus.Trialing, false)]
    [InlineData(SubscriptionStatus.Cancelled, false)]
    [InlineData(SubscriptionStatus.Expired, false)]
    [InlineData(SubscriptionStatus.Active, true)]
    [InlineData(SubscriptionStatus.PastDue, true)]
    [InlineData(SubscriptionStatus.GracePeriod, true)]
    [InlineData(SubscriptionStatus.Suspended, true)]
    public void IsAllowed_matches_subscription_lifecycle(SubscriptionStatus? status, bool expected)
    {
        Assert.Equal(expected, CompanyAdminLoginAccess.IsAllowed(status));
    }

    [Fact]
    public void Open_trial_can_sign_in_and_dateless_or_ended_trial_cannot()
    {
        var now = new DateTimeOffset(2026, 9, 12, 12, 0, 0, TimeSpan.Zero);
        Assert.True(CompanyAdminLoginAccess.IsAllowed(SubscriptionStatus.Trialing, now.AddDays(7), now));
        Assert.False(CompanyAdminLoginAccess.IsAllowed(SubscriptionStatus.Trialing, null, now));
        Assert.False(CompanyAdminLoginAccess.IsAllowed(SubscriptionStatus.Trialing, now.AddHours(-1), now));
        Assert.False(CompanyAdminLoginAccess.IsAllowed(SubscriptionStatus.Expired, now.AddDays(7), now));
        Assert.False(CompanyAdminLoginAccess.IsAllowed(SubscriptionStatus.Cancelled, now.AddDays(7), now));
    }
}
