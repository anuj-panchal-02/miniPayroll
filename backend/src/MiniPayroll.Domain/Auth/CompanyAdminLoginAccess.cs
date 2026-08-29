using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Auth;

public static class CompanyAdminLoginAccess
{
    public static bool IsAllowed(SubscriptionStatus? status) =>
        status is SubscriptionStatus.Active
            or SubscriptionStatus.PastDue
            or SubscriptionStatus.Suspended;
}
