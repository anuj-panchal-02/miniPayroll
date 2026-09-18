namespace MiniPayroll.Domain.Subscriptions;

public sealed record EmployeeUsage(
    int CurrentUsage,
    int MaximumAllowed,
    int Remaining,
    bool CanAdd);
