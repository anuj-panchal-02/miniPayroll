namespace MiniPayroll.Domain.Auth;

public static class SuperadminSeedRules
{
    public static bool MayBootstrap(bool isDevelopment, bool allowBootstrap) =>
        isDevelopment || allowBootstrap;

    public static string RequirePassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            throw new InvalidOperationException(
                "Seed:SuperadminPassword is required to bootstrap Superadmin.");
        }

        return password;
    }
}
