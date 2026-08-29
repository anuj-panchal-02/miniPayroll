using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MiniPayroll.Api.Auth;
using MiniPayroll.Domain.Auth;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Infrastructure.Identity;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Api.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth");

        group.MapPost("/login", Login);
        group.MapGet("/me", Me).RequireAuthorization();
        group.MapPost("/change-password", ChangePassword).RequireAuthorization();

        return routes;
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signIn,
        JwtTokenService tokens,
        IConfiguration configuration,
        MiniPayrollDbContext db,
        CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Results.Json(new { error = "Invalid email or password." }, statusCode: StatusCodes.Status401Unauthorized);
        }

        var check = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (check.IsLockedOut)
        {
            return Results.Json(new { error = "Account is locked. Try again later." }, statusCode: StatusCodes.Status423Locked);
        }

        if (!check.Succeeded)
        {
            return Results.Json(new { error = "Invalid email or password." }, statusCode: StatusCodes.Status401Unauthorized);
        }

        var roles = await users.GetRolesAsync(user);
        var isSuperadmin = roles.Contains(RoleNames.Superadmin);
        var allowWithoutMfa = configuration.GetValue("Security:AllowSuperadminWithoutMfa", false);

        if (isSuperadmin && !user.TwoFactorEnabled && !allowWithoutMfa)
        {
            return Results.Json(
                new { error = "Superadmin MFA is required.", requiresMfaEnrollment = true },
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (!isSuperadmin)
        {
            var status = user.CompanyId is { } companyId
                ? await db.Subscriptions.AsNoTracking()
                    .IgnoreQueryFilters()
                    .Where(s => s.CompanyId == companyId)
                    .Select(s => (SubscriptionStatus?)s.Status)
                    .FirstOrDefaultAsync(cancellationToken)
                : null;

            if (!CompanyAdminLoginAccess.IsAllowed(status))
            {
                var message = status == SubscriptionStatus.Cancelled
                    ? "This company can no longer sign in."
                    : "This company is not active yet. Sign in after it is activated.";
                return Results.Json(new { error = message }, statusCode: StatusCodes.Status403Forbidden);
            }
        }

        var setupState = await AuthSetupStateResolver.ResolveForLoginAsync(
            db,
            user.CompanyId,
            isSuperadmin,
            cancellationToken);
        if (setupState is not { } resolvedSetupState)
        {
            return AuthSetupStateHttpResults.Forbidden();
        }

        var token = tokens.CreateToken(user, roles);
        return Results.Ok(new AuthResponse(
            token,
            user.Email ?? request.Email,
            roles,
            user.CompanyId,
            user.MustChangePassword,
            isSuperadmin && !user.TwoFactorEnabled,
            resolvedSetupState.IsSetupComplete,
            resolvedSetupState.SetupStep.ToString()));
    }

    private static async Task<IResult> Me(
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> users,
        MiniPayrollDbContext db,
        CancellationToken cancellationToken)
    {
        var user = await users.GetUserAsync(principal);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var roles = await users.GetRolesAsync(user);
        var setupState = await AuthSetupStateResolver.ResolveForAuthenticatedRequestAsync(
            db,
            user.CompanyId,
            roles.Contains(RoleNames.Superadmin),
            cancellationToken);
        if (setupState is not { } resolvedSetupState)
        {
            return AuthSetupStateHttpResults.Forbidden();
        }

        return Results.Ok(new MeResponse(
            user.Id,
            user.Email ?? string.Empty,
            roles,
            user.CompanyId,
            user.MustChangePassword,
            user.TwoFactorEnabled,
            resolvedSetupState.IsSetupComplete,
            resolvedSetupState.SetupStep.ToString()));
    }

    private static async Task<IResult> ChangePassword(
        ChangePasswordRequest request,
        ClaimsPrincipal principal,
        UserManager<ApplicationUser> users)
    {
        var user = await users.GetUserAsync(principal);
        if (user is null)
        {
            return Results.Unauthorized();
        }

        var result = await users.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return Results.BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }

        user.MustChangePassword = false;
        await users.UpdateAsync(user);
        return Results.NoContent();
    }

    public sealed record LoginRequest(
        [Required, EmailAddress] string Email,
        [Required] string Password);

    public sealed record ChangePasswordRequest(
        [Required] string CurrentPassword,
        [Required] string NewPassword);

    public sealed record AuthResponse(
        string Token,
        string Email,
        IList<string> Roles,
        Guid? CompanyId,
        bool MustChangePassword,
        bool RequiresMfaEnrollment,
        bool IsSetupComplete,
        string SetupStep);

    public sealed record MeResponse(
        Guid Id,
        string Email,
        IList<string> Roles,
        Guid? CompanyId,
        bool MustChangePassword,
        bool TwoFactorEnabled,
        bool IsSetupComplete,
        string SetupStep);
}
