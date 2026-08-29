using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Api.Auth;

public sealed class PasswordChangeCompletedRequirement : IAuthorizationRequirement;

/// <summary>
/// Reads the persisted flag instead of the <c>must_change_password</c> token claim, which stays
/// stale for the lifetime of a token that was issued before the password was changed.
/// </summary>
public sealed class PasswordChangeCompletedHandler(MiniPayrollDbContext db)
    : AuthorizationHandler<PasswordChangeCompletedRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PasswordChangeCompletedRequirement requirement)
    {
        if (!Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            return;
        }

        var mustChangePassword = await db.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => (bool?)user.MustChangePassword)
            .SingleOrDefaultAsync();

        if (mustChangePassword == false)
        {
            context.Succeed(requirement);
        }
    }
}

public static class CompanySetupAuthorization
{
    public static void Configure(AuthorizationPolicyBuilder policy)
    {
        ArgumentNullException.ThrowIfNull(policy);

        policy.RequireRole(RoleNames.CompanyAdmin)
            .AddRequirements(new PasswordChangeCompletedRequirement());
    }

    public static AuthorizationPolicy Policy()
    {
        var policy = new AuthorizationPolicyBuilder();
        Configure(policy);
        return policy.Build();
    }
}
