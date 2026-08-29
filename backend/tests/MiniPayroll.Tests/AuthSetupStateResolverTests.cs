using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Auth;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public class AuthSetupStateResolverTests
{
    [Fact]
    public async Task Login_returns_persisted_incomplete_state_for_activated_company_admin_without_tenant_claim()
    {
        var company = NewCompany(isSetupComplete: false, CompanySetupStep.CompanyDetails);
        var noTenantClaim = new StaticTenantContext
        {
            UserId = Guid.NewGuid(),
            CompanyId = null,
            IsSuperadmin = false
        };
        await using var db = TestDb.Create(noTenantClaim, UniqueDatabase());
        db.Companies.Add(company);
        db.AddRange(NewPlanAndSubscription(company, SubscriptionStatus.Active));
        await db.SaveChangesAsync();

        var status = await db.Subscriptions
            .IgnoreQueryFilters()
            .Where(subscription => subscription.CompanyId == company.Id)
            .Select(subscription => (SubscriptionStatus?)subscription.Status)
            .SingleAsync();
        var state = await AuthSetupStateResolver.ResolveForLoginAsync(
            db,
            company.Id,
            isSuperadmin: false);

        Assert.True(CompanyAdminLoginAccess.IsAllowed(status));
        Assert.True(state.HasValue);
        Assert.False(state.Value.IsSetupComplete);
        Assert.Equal(CompanySetupStep.CompanyDetails, state.Value.SetupStep);
    }

    [Fact]
    public async Task Login_returns_persisted_complete_state_for_completed_company_admin()
    {
        var company = NewCompany(isSetupComplete: true, CompanySetupStep.Complete);
        await using var db = TestDb.Create(NullTenantContext.Instance, UniqueDatabase());
        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var state = await AuthSetupStateResolver.ResolveForLoginAsync(
            db,
            company.Id,
            isSuperadmin: false);

        Assert.True(state.HasValue);
        Assert.True(state.Value.IsSetupComplete);
        Assert.Equal(CompanySetupStep.Complete, state.Value.SetupStep);
    }

    [Fact]
    public async Task Superadmin_defaults_to_complete_without_company_lookup()
    {
        await using var db = TestDb.Create(NullTenantContext.Instance, UniqueDatabase());

        var state = await AuthSetupStateResolver.ResolveForLoginAsync(
            db,
            trustedCompanyId: null,
            isSuperadmin: true);

        Assert.True(state.HasValue);
        Assert.True(state.Value.IsSetupComplete);
        Assert.Equal(CompanySetupStep.Complete, state.Value.SetupStep);
    }

    [Fact]
    public async Task Authenticated_request_uses_tenant_filter_and_trusted_company_id()
    {
        var trustedCompany = NewCompany(isSetupComplete: false, CompanySetupStep.PayrollSettings);
        var otherCompany = NewCompany(isSetupComplete: true, CompanySetupStep.Complete);
        var database = UniqueDatabase();

        await using (var setup = TestDb.Create(NullTenantContext.Instance, database))
        {
            setup.Companies.AddRange(trustedCompany, otherCompany);
            await setup.SaveChangesAsync();
        }

        var tenant = new StaticTenantContext
        {
            UserId = Guid.NewGuid(),
            CompanyId = trustedCompany.Id,
            IsSuperadmin = false
        };
        await using var db = TestDb.Create(tenant, database);

        var state = await AuthSetupStateResolver.ResolveForAuthenticatedRequestAsync(
            db,
            trustedCompany.Id,
            isSuperadmin: false);

        Assert.True(state.HasValue);
        Assert.False(state.Value.IsSetupComplete);
        Assert.Equal(CompanySetupStep.PayrollSettings, state.Value.SetupStep);
    }

    [Fact]
    public async Task Login_returns_no_state_when_trusted_company_is_missing()
    {
        await using var db = TestDb.Create(NullTenantContext.Instance, UniqueDatabase());

        var state = await AuthSetupStateResolver.ResolveForLoginAsync(
            db,
            Guid.NewGuid(),
            isSuperadmin: false);

        Assert.Null(state);
    }

    [Fact]
    public async Task Authenticated_request_returns_no_state_when_trusted_company_is_tenant_invisible()
    {
        var tenantCompany = NewCompany(isSetupComplete: false, CompanySetupStep.CompanyDetails);
        var otherCompany = NewCompany(isSetupComplete: true, CompanySetupStep.Complete);
        var database = UniqueDatabase();

        await using (var setup = TestDb.Create(NullTenantContext.Instance, database))
        {
            setup.Companies.AddRange(tenantCompany, otherCompany);
            await setup.SaveChangesAsync();
        }

        var tenant = new StaticTenantContext
        {
            UserId = Guid.NewGuid(),
            CompanyId = tenantCompany.Id,
            IsSuperadmin = false
        };
        await using var db = TestDb.Create(tenant, database);

        var state = await AuthSetupStateResolver.ResolveForAuthenticatedRequestAsync(
            db,
            otherCompany.Id,
            isSuperadmin: false);

        Assert.Null(state);
    }

    private static Company NewCompany(bool isSetupComplete, CompanySetupStep setupStep) => new()
    {
        Id = Guid.NewGuid(),
        Name = "ABC Traders",
        ContactEmail = $"{Guid.NewGuid():N}@example.com",
        IsSetupComplete = isSetupComplete,
        SetupStep = setupStep,
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static object[] NewPlanAndSubscription(
        Company company,
        SubscriptionStatus status)
    {
        var plan = new Plan
        {
            Id = Guid.NewGuid(),
            Name = $"Plan-{Guid.NewGuid():N}",
            PricePerEmployee = 100,
            DefaultEmployeeLimit = 10
        };
        var subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            PlanId = plan.Id,
            Company = company,
            Plan = plan,
            Status = status,
            EmployeeLimit = 10
        };

        return [plan, subscription];
    }

    private static string UniqueDatabase() =>
        $"auth-setup-state-{Guid.NewGuid():N}";
}
