using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public class TenantIsolationTests
{
    [Fact]
    public async Task Company_admin_cannot_see_another_company()
    {
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();
        const string database = "tenant-isolation";

        await using (var setup = TestDb.Create(NullTenantContext.Instance, database))
        {
            setup.Companies.AddRange(
                NewCompany(companyA, "ABC Traders"),
                NewCompany(companyB, "Other Co"));
            await setup.SaveChangesAsync();
        }

        await using var tenantDb = TestDb.Create(
            new StaticTenantContext
            {
                UserId = Guid.NewGuid(),
                CompanyId = companyA,
                IsSuperadmin = false
            },
            database);

        var visible = await tenantDb.Companies.ToListAsync();

        Assert.Single(visible);
        Assert.Equal(companyA, visible[0].Id);
        Assert.Equal("ABC Traders", visible[0].Name);
    }

    [Fact]
    public async Task Superadmin_can_see_all_companies()
    {
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();
        const string database = "tenant-superadmin";

        await using (var setup = TestDb.Create(NullTenantContext.Instance, database))
        {
            setup.Companies.AddRange(
                NewCompany(companyA, "ABC Traders"),
                NewCompany(companyB, "Other Co"));
            await setup.SaveChangesAsync();
        }

        await using var superadminDb = TestDb.Create(
            new StaticTenantContext
            {
                UserId = Guid.NewGuid(),
                IsSuperadmin = true
            },
            database);

        var visible = await superadminDb.Companies.OrderBy(c => c.Name).ToListAsync();

        Assert.Equal(2, visible.Count);
    }

    private static Company NewCompany(Guid id, string name) => new()
    {
        Id = id,
        Name = name,
        ContactEmail = $"{id:N}@example.com",
        CreatedAt = DateTimeOffset.UtcNow
    };
}
