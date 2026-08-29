namespace MiniPayroll.Api.Storage;

public sealed record StoredCompanyLogo(string RelativePath);

public interface ICompanyLogoStorage
{
    Task<StoredCompanyLogo> SaveAsync(
        Guid tenantCompanyId,
        Stream content,
        string? originalFileName,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string relativePath,
        CancellationToken cancellationToken = default);
}
