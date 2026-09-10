namespace MiniPayroll.Domain.Auth;

public static class TemporaryPasswordRules
{
    public static bool IsProvided(string? password) =>
        !string.IsNullOrWhiteSpace(password);
}
