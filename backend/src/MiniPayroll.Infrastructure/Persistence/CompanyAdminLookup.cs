using Microsoft.EntityFrameworkCore;
using MiniPayroll.Infrastructure.Identity;

namespace MiniPayroll.Infrastructure.Persistence;

public readonly record struct CompanyAdminLookupResult(bool HasAdmin, string? Email);

public static class CompanyAdminLookup
{
    public static async Task<CompanyAdminLookupResult> GetAsync(
        MiniPayrollDbContext db,
        Guid companyId,
        CancellationToken cancellationToken = default)
    {
        var email = await db.Users
            .AsNoTracking()
            .Where(user => user.CompanyId == companyId)
            .Select(user => user.Email)
            .FirstOrDefaultAsync(cancellationToken);

        return string.IsNullOrEmpty(email)
            ? new CompanyAdminLookupResult(false, null)
            : new CompanyAdminLookupResult(true, email);
    }

    public static async Task<HashSet<Guid>> CompanyIdsWithAdminAsync(
        MiniPayrollDbContext db,
        CancellationToken cancellationToken = default)
    {
        var ids = await db.Users
            .AsNoTracking()
            .Where(user => user.CompanyId != null)
            .Select(user => user.CompanyId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        return [.. ids];
    }
}
