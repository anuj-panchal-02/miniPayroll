using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public sealed class PlanSettingsServiceTests
{
    [Fact]
    public async Task Get_and_update_are_superadmin_only()
    {
        var fixture = await FixtureAsync();
        var companyAdmin = new StaticTenantContext
        {
            UserId = Guid.NewGuid(),
            CompanyId = Guid.NewGuid(),
            IsSuperadmin = false
        };
        await using var adminDb = TestDb.Create(companyAdmin, fixture.Database);
        var admins = new PlanSettingsService(adminDb, companyAdmin);
        Assert.Equal(PlanSettingsStatus.Forbidden, (await admins.GetAsync()).Status);
        Assert.Equal(PlanSettingsStatus.Forbidden, (await admins.UpdateAsync(59m)).Status);
    }

    [Fact]
    public async Task Superadmin_can_change_the_basic_price()
    {
        var fixture = await FixtureAsync();
        var updated = await fixture.Plans.UpdateAsync(59m);
        Assert.Equal(PlanSettingsStatus.Success, updated.Status);
        Assert.Equal(59m, updated.Plan!.PricePerEmployee);
        Assert.Equal(PlatformLimits.DefaultPlanName, updated.Plan.Name);

        var loaded = await fixture.Plans.GetAsync();
        Assert.Equal(59m, loaded.Plan!.PricePerEmployee);
        await using var db = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        Assert.Equal(1, await db.AuditLogs.CountAsync(item => item.Action == AuditActions.PlanPriceChange));
        var prices = await db.PlanPrices.OrderBy(price => price.EffectiveFrom).ToListAsync();
        Assert.Equal(59m, Assert.Single(prices).Amount);
    }

    [Fact]
    public async Task Price_update_closes_the_previous_monthly_row()
    {
        var fixture = await FixtureAsync();
        Assert.Equal(PlanSettingsStatus.Success, (await fixture.Plans.UpdateAsync(49m)).Status);
        Assert.Equal(PlanSettingsStatus.Success, (await fixture.Plans.UpdateAsync(59m)).Status);

        await using var db = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        var prices = await db.PlanPrices
            .Where(price => price.BillingCycle == MiniPayroll.Domain.Enums.BillingCycle.Monthly)
            .OrderBy(price => price.EffectiveFrom)
            .ToListAsync();
        Assert.Equal(2, prices.Count);
        Assert.Equal(49m, prices[0].Amount);
        Assert.False(prices[0].IsActive);
        Assert.NotNull(prices[0].EffectiveTo);
        Assert.Equal(59m, prices[1].Amount);
        Assert.True(prices[1].IsActive);
        Assert.Null(prices[1].EffectiveTo);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10.001)]
    public async Task Rejects_invalid_prices(decimal price)
    {
        var fixture = await FixtureAsync();
        Assert.Equal(PlanSettingsStatus.InvalidInput, (await fixture.Plans.UpdateAsync(price)).Status);
    }

    private sealed record Fixture(string Database, PlanSettingsService Plans);

    private static async Task<Fixture> FixtureAsync()
    {
        var database = $"plan-{Guid.NewGuid():N}";
        await using (var db = TestDb.Create(NullTenantContext.Instance, database))
        {
            db.Plans.Add(new Plan
            {
                Id = Guid.NewGuid(),
                Code = PlatformLimits.DefaultPlanCode,
                Name = PlatformLimits.DefaultPlanName,
                IsActive = true,
                MaxActiveEmployees = 50,
                PricePerEmployee = 49m,
                DefaultEmployeeLimit = 50,
                IsPublic = true
            });
            await db.SaveChangesAsync();
        }

        var tenant = new StaticTenantContext { UserId = Guid.NewGuid(), IsSuperadmin = true };
        return new Fixture(database, new PlanSettingsService(TestDb.Create(tenant, database), tenant));
    }
}
