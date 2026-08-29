using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using MiniPayroll.Domain.Tenancy;

namespace MiniPayroll.Infrastructure.Persistence;

public sealed class MiniPayrollDbContextFactory : IDesignTimeDbContextFactory<MiniPayrollDbContext>
{
    public MiniPayrollDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<MiniPayrollDbContext>()
            .UseSqlServer(
                "Server=(localdb)\\mssqllocaldb;Database=MiniPayroll;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True")
            .Options;

        return new MiniPayrollDbContext(
            options,
            NullTenantContext.Instance,
            new EphemeralDataProtectionProvider());
    }
}
