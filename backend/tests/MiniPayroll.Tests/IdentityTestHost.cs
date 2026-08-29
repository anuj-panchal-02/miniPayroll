using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Identity;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

internal static class IdentityTestHost
{
    public static async Task<IServiceProvider> CreateAsync(string databaseName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ITenantContext>(NullTenantContext.Instance);
        services.AddDbContext<MiniPayrollDbContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.User.RequireUniqueEmail = true;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<MiniPayrollDbContext>();
        services.AddScoped<CompanyAdminService>();

        var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        if (!await roles.RoleExistsAsync(RoleNames.CompanyAdmin))
        {
            await roles.CreateAsync(new ApplicationRole(RoleNames.CompanyAdmin));
        }

        return provider;
    }
}
