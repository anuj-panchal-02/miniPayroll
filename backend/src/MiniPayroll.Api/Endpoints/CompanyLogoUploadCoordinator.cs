using MiniPayroll.Api.Storage;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Api.Endpoints;

public sealed class CompanyLogoUploadCoordinator(
    ICompanyLogoStorage storage,
    ILogger<CompanyLogoUploadCoordinator> logger)
{
    public async Task<CompanySetupResult> SaveAsync(
        CompanySetupState currentState,
        Stream content,
        string? originalFileName,
        Func<string, CancellationToken, Task<CompanySetupResult>> persist,
        CancellationToken cancellationToken = default)
    {
        if (currentState.IsSetupComplete)
        {
            return new CompanySetupResult(CompanySetupStatus.AlreadyComplete, currentState);
        }

        var saved = await storage.SaveAsync(
            currentState.CompanyId,
            content,
            originalFileName,
            cancellationToken);

        CompanySetupResult persisted;
        try
        {
            persisted = await persist(saved.RelativePath, cancellationToken);
        }
        catch
        {
            await TryDeleteAsync(saved.RelativePath);
            throw;
        }

        if (persisted.Status != CompanySetupStatus.Success)
        {
            await TryDeleteAsync(saved.RelativePath);
            return persisted;
        }

        var previousPath = NormalizeStoragePath(currentState.LogoPath);
        if (!string.IsNullOrEmpty(previousPath)
            && !string.Equals(
                previousPath,
                saved.RelativePath,
                StringComparison.OrdinalIgnoreCase))
        {
            await TryDeleteAsync(previousPath);
        }

        return persisted;
    }

    private async Task TryDeleteAsync(string relativePath)
    {
        try
        {
            await storage.DeleteAsync(relativePath, CancellationToken.None);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Could not delete company logo at {LogoPath}.",
                relativePath);
        }
    }

    private static string? NormalizeStoragePath(string? path)
    {
        var normalized = path?.Trim().Replace('\\', '/').TrimStart('/');
        return normalized?.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase) == true
            ? normalized["uploads/".Length..]
            : normalized;
    }
}
