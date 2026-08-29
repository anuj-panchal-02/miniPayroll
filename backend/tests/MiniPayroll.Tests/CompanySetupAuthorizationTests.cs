using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniPayroll.Api.Auth;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Identity;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public sealed class CompanySetupAuthorizationTests
{
    [Fact]
    public async Task The_same_token_is_denied_before_and_allowed_after_the_password_change()
    {
        var database = UniqueDatabase();
        var userId = Guid.NewGuid();
        await SeedAdminAsync(database, userId, mustChangePassword: true);
        var provider = BuildProvider(database);
        var principal = CompanyAdmin(userId, mustChangePasswordClaim: "true");

        var beforeChange = await AuthorizeAsync(provider, principal);
        await CompletePasswordChangeAsync(database, userId);
        var afterChange = await AuthorizeAsync(provider, principal);

        Assert.False(beforeChange.Succeeded);
        Assert.True(afterChange.Succeeded);
    }

    [Fact]
    public async Task A_stale_claim_cannot_grant_access_while_the_stored_flag_is_set()
    {
        var database = UniqueDatabase();
        var userId = Guid.NewGuid();
        await SeedAdminAsync(database, userId, mustChangePassword: true);
        var provider = BuildProvider(database);

        var result = await AuthorizeAsync(
            provider,
            CompanyAdmin(userId, mustChangePasswordClaim: "false"));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Unknown_subjects_are_denied()
    {
        var database = UniqueDatabase();
        await SeedAdminAsync(database, Guid.NewGuid(), mustChangePassword: false);
        var provider = BuildProvider(database);

        var result = await AuthorizeAsync(provider, CompanyAdmin(Guid.NewGuid()));

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task The_company_admin_role_is_still_required()
    {
        var database = UniqueDatabase();
        var userId = Guid.NewGuid();
        await SeedAdminAsync(database, userId, mustChangePassword: false);
        var provider = BuildProvider(database);

        var result = await AuthorizeAsync(
            provider,
            Principal(userId, RoleNames.Superadmin, mustChangePasswordClaim: "false"));

        Assert.False(result.Succeeded);
    }

    private static async Task<AuthorizationResult> AuthorizeAsync(
        IServiceProvider provider,
        ClaimsPrincipal principal)
    {
        await using var scope = provider.CreateAsyncScope();
        var authorization = scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        return await authorization.AuthorizeAsync(
            principal,
            resource: null,
            CompanySetupAuthorization.Policy());
    }

    private static ServiceProvider BuildProvider(string database)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();
        services.AddSingleton<ITenantContext>(NullTenantContext.Instance);
        services.AddDbContext<MiniPayrollDbContext>(options =>
            options.UseInMemoryDatabase(database));
        services.AddScoped<IAuthorizationHandler, PasswordChangeCompletedHandler>();
        return services.BuildServiceProvider();
    }

    private static async Task SeedAdminAsync(
        string database,
        Guid userId,
        bool mustChangePassword)
    {
        await using var db = TestDb.Create(NullTenantContext.Instance, database);
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            Email = "admin@example.com",
            UserName = "admin@example.com",
            CompanyId = Guid.NewGuid(),
            MustChangePassword = mustChangePassword,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task CompletePasswordChangeAsync(string database, Guid userId)
    {
        await using var db = TestDb.Create(NullTenantContext.Instance, database);
        var user = await db.Users.SingleAsync(user => user.Id == userId);
        user.MustChangePassword = false;
        await db.SaveChangesAsync();
    }

    private static ClaimsPrincipal CompanyAdmin(
        Guid userId,
        string mustChangePasswordClaim = "true") =>
        Principal(userId, RoleNames.CompanyAdmin, mustChangePasswordClaim);

    private static ClaimsPrincipal Principal(
        Guid userId,
        string role,
        string mustChangePasswordClaim) =>
        new(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role),
                new Claim("must_change_password", mustChangePasswordClaim)
            ],
            authenticationType: "Test"));

    private static string UniqueDatabase() => $"setup-authorization-{Guid.NewGuid():N}";
}
