using Microsoft.Extensions.DependencyInjection;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Identity;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public class CompanyAdminTests
{
    [Fact]
    public async Task Company_without_admin_reports_hasAdmin_false()
    {
        var companyId = Guid.NewGuid();
        await using var db = TestDb.Create(NullTenantContext.Instance, nameof(Company_without_admin_reports_hasAdmin_false));
        db.Companies.Add(NewCompany(companyId, "ABC Traders"));
        await db.SaveChangesAsync();

        var lookup = await CompanyAdminLookup.GetAsync(db, companyId);

        Assert.False(lookup.HasAdmin);
        Assert.Null(lookup.Email);
    }

    [Fact]
    public async Task After_create_GET_reports_hasAdmin_and_email()
    {
        var host = await IdentityTestHost.CreateAsync(nameof(After_create_GET_reports_hasAdmin_and_email));
        await using var scope = host.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MiniPayrollDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<CompanyAdminService>();

        var companyId = Guid.NewGuid();
        db.Companies.Add(NewCompany(companyId, "ABC Traders"));
        await db.SaveChangesAsync();

        var created = await service.CreateAsync(
            companyId,
            "owner@abctraders.example",
            actorUserId: Guid.NewGuid());

        Assert.Equal(CompanyAdminCreateStatus.Created, created.Status);
        Assert.NotNull(created.Response);

        var lookup = await CompanyAdminLookup.GetAsync(db, companyId);

        Assert.True(lookup.HasAdmin);
        Assert.Equal("owner@abctraders.example", lookup.Email);
    }

    [Fact]
    public async Task Second_admin_for_the_same_company_is_rejected()
    {
        var host = await IdentityTestHost.CreateAsync(nameof(Second_admin_for_the_same_company_is_rejected));
        await using var scope = host.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MiniPayrollDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<CompanyAdminService>();

        var companyId = Guid.NewGuid();
        db.Companies.Add(NewCompany(companyId, "ABC Traders"));
        await db.SaveChangesAsync();

        var first = await service.CreateAsync(companyId, "owner@abctraders.example", Guid.NewGuid());
        Assert.Equal(CompanyAdminCreateStatus.Created, first.Status);

        var second = await service.CreateAsync(companyId, "other@abctraders.example", Guid.NewGuid());

        Assert.Equal(CompanyAdminCreateStatus.AlreadyHasAdmin, second.Status);
        Assert.Null(second.Response);
    }

    private static Company NewCompany(Guid id, string name) => new()
    {
        Id = id,
        Name = name,
        ContactEmail = $"{id:N}@example.com",
        CreatedAt = DateTimeOffset.UtcNow,
        Subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            CompanyId = id,
            PlanId = Guid.NewGuid(),
            Status = SubscriptionStatus.Pending,
            EmployeeLimit = 9,
            GracePeriodDays = PlatformLimits.DefaultGracePeriodDays
        }
    };
}
