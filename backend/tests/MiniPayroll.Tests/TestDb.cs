using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

internal static class TestDb
{
    private static readonly IDataProtectionProvider Protection =
        new EphemeralDataProtectionProvider();

    public static MiniPayrollDbContext Create(
        ITenantContext tenant,
        string databaseName = "minipayroll-tests",
        IInterceptor? interceptor = null)
    {
        var options = new DbContextOptionsBuilder<MiniPayrollDbContext>()
            .UseInMemoryDatabase(databaseName);

        if (interceptor is not null)
        {
            options.AddInterceptors(interceptor);
        }

        return new MiniPayrollDbContext(options.Options, tenant, Protection);
    }
}
