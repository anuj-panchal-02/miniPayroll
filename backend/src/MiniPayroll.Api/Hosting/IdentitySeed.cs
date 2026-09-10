using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Auth;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
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

        if (!await db.Plans.AnyAsync(p => p.Name == PlatformLimits.DefaultPlanName, cancellationToken))
        {
            db.Plans.Add(new Plan
            {
                Id = Guid.NewGuid(),
                Name = PlatformLimits.DefaultPlanName,
                PricePerEmployee = PlatformLimits.DefaultPricePerEmployee,
                DefaultEmployeeLimit = PlatformLimits.DefaultEmployeeLimit,
                IsPublic = true
            });
            await db.SaveChangesAsync(cancellationToken);
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
}
