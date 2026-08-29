using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Enums;

namespace MiniPayroll.Infrastructure.Persistence;

public readonly record struct AuthSetupState(
    bool IsSetupComplete,
    CompanySetupStep SetupStep);

public static class AuthSetupStateResolver
{
    public static Task<AuthSetupState?> ResolveForLoginAsync(
        MiniPayrollDbContext db,
        Guid? trustedCompanyId,
        bool isSuperadmin,
        CancellationToken cancellationToken = default) =>
        ResolveAsync(
            db,
            trustedCompanyId,
            isSuperadmin,
            ignoreQueryFilters: true,
            cancellationToken);

    public static Task<AuthSetupState?> ResolveForAuthenticatedRequestAsync(
        MiniPayrollDbContext db,
        Guid? trustedCompanyId,
        bool isSuperadmin,
        CancellationToken cancellationToken = default) =>
        ResolveAsync(
            db,
            trustedCompanyId,
            isSuperadmin,
            ignoreQueryFilters: false,
            cancellationToken);

    private static async Task<AuthSetupState?> ResolveAsync(
        MiniPayrollDbContext db,
        Guid? trustedCompanyId,
        bool isSuperadmin,
        bool ignoreQueryFilters,
        CancellationToken cancellationToken)
    {
        if (isSuperadmin)
        {
            return new AuthSetupState(true, CompanySetupStep.Complete);
        }

        if (trustedCompanyId is not { } companyId)
        {
            return null;
        }

        var companies = db.Companies.AsNoTracking();
        if (ignoreQueryFilters)
        {
            companies = companies.IgnoreQueryFilters();
        }

        var company = await companies
            .Where(company => company.Id == companyId)
            .Select(company => new
            {
                company.IsSetupComplete,
                company.SetupStep
            })
            .SingleOrDefaultAsync(cancellationToken);

        return company is null
            ? null
            : new AuthSetupState(company.IsSetupComplete, company.SetupStep);
    }
}
