using MiniPayroll.Domain.Auth;
using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Tests;

public class CompanyAdminLoginAccessTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData(SubscriptionStatus.Pending, false)]
    [InlineData(SubscriptionStatus.Cancelled, false)]
    [InlineData(SubscriptionStatus.Active, true)]
    [InlineData(SubscriptionStatus.PastDue, true)]
    [InlineData(SubscriptionStatus.Suspended, true)]
    public void IsAllowed_matches_subscription_lifecycle(SubscriptionStatus? status, bool expected)
    {
        Assert.Equal(expected, CompanyAdminLoginAccess.IsAllowed(status));
    }
}
