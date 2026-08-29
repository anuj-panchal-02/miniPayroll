using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Domain.Auth;

public static class SubscriptionMutationRules
{
    public static bool CanMutate(SubscriptionStatus? status) =>
        status is SubscriptionStatus.Active or SubscriptionStatus.PastDue;
}
