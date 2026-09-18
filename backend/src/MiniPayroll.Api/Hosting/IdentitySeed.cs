using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Auth;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Subscriptions;
using MiniPayroll.Infrastructure.Identity;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Api.Hosting;

public static class IdentitySeed
{
    public static async Task EnsureSeededAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var db = services.GetRequiredService<MiniPayrollDbContext>();
        var roles = services.GetRequiredService<RoleManager<ApplicationRole>>();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var configuration = services.GetRequiredService<IConfiguration>();

        if (!await roles.RoleExistsAsync(RoleNames.Superadmin))
        {
            await roles.CreateAsync(new ApplicationRole(RoleNames.Superadmin));
        }

        if (!await roles.RoleExistsAsync(RoleNames.CompanyAdmin))
        {
            await roles.CreateAsync(new ApplicationRole(RoleNames.CompanyAdmin));
        }

        if (!await db.Plans.AnyAsync(
                p => p.Code == PlatformLimits.DefaultPlanCode || p.Name == PlatformLimits.DefaultPlanName,
                cancellationToken))
        {
            var plan = new Plan
            {
                Id = Guid.NewGuid(),
                Code = PlatformLimits.DefaultPlanCode,
                Name = PlatformLimits.DefaultPlanName,
                IsActive = true,
                MaxActiveEmployees = PlatformLimits.DefaultEmployeeLimit,
                TrialDays = 0,
                PricePerEmployee = PlatformLimits.DefaultPricePerEmployee,
                DefaultEmployeeLimit = PlatformLimits.DefaultEmployeeLimit,
                IsPublic = true
            };
            plan.Prices.Add(new PlanPrice
            {
                Id = Guid.NewGuid(),
                PlanId = plan.Id,
                BillingCycle = BillingCycle.Monthly,
                Amount = PlatformLimits.DefaultPricePerEmployee,
                Currency = PlatformLimits.CurrencyCode,
                EffectiveFrom = SubscriptionSchemaBackfill.OpenPriceWindow,
                IsActive = true
            });
            foreach (var code in PlanFeatureCodes.CoreEnabled)
            {
                plan.Features.Add(new PlanFeature
                {
                    Id = Guid.NewGuid(),
                    PlanId = plan.Id,
                    Code = code,
                    IsEnabled = true
                });
            }
            db.Plans.Add(plan);
            await db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            await EnsureCoreFeaturesAsync(db, cancellationToken);
        }

        await LocationSeed.EnsureSeededAsync(db, cancellationToken);

        var email = configuration["Seed:SuperadminEmail"] ?? "superadmin@minipayroll.local";

        if (await users.FindByEmailAsync(email) is not null)
        {
            return;
        }

        var environment = services.GetRequiredService<IHostEnvironment>();
        var allowBootstrap = configuration.GetValue("Seed:AllowBootstrap", false);
        if (!SuperadminSeedRules.MayBootstrap(environment.IsDevelopment(), allowBootstrap))
        {
            throw new InvalidOperationException(
                "Superadmin bootstrap is disabled. Set Seed:AllowBootstrap=true and Seed:SuperadminPassword.");
        }

        var password = SuperadminSeedRules.RequirePassword(configuration["Seed:SuperadminPassword"]);

        var superadmin = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            MustChangePassword = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await users.CreateAsync(superadmin, password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to seed Superadmin: {errors}");
        }

        await users.AddToRoleAsync(superadmin, RoleNames.Superadmin);
    }

    private static async Task EnsureCoreFeaturesAsync(
        MiniPayrollDbContext db,
        CancellationToken cancellationToken)
    {
        var plan = await db.Plans
            .Include(item => item.Features)
            .SingleOrDefaultAsync(
                item => item.Code == PlatformLimits.DefaultPlanCode
                    || item.Name == PlatformLimits.DefaultPlanName,
                cancellationToken);
        if (plan is null)
        {
            return;
        }

        var changed = false;
        foreach (var feature in plan.Features.Where(item =>
                     string.Equals(item.Code, "payroll", StringComparison.OrdinalIgnoreCase)
                     && item.Code != PlanFeatureCodes.Payroll))
        {
            feature.Code = PlanFeatureCodes.Payroll;
            feature.IsEnabled = true;
            changed = true;
        }

        foreach (var code in PlanFeatureCodes.CoreEnabled)
        {
            if (plan.Features.Any(item => string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            plan.Features.Add(new PlanFeature
            {
                Id = Guid.NewGuid(),
                PlanId = plan.Id,
                Code = code,
                IsEnabled = true
            });
            changed = true;
        }

        if (changed)
        {
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
