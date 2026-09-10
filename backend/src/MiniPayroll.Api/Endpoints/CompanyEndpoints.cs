using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Auth;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Infrastructure.Identity;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Api.Endpoints;

public static class CompanyEndpoints
{
    public static IEndpointRouteBuilder MapCompanyEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/companies").RequireAuthorization(policy =>
            policy.RequireRole(RoleNames.Superadmin));

        group.MapGet("/", List);
        group.MapGet("/{id:guid}", Get);
        group.MapPost("/", Create);
        group.MapPost("/{id:guid}/admin", CreateAdmin);
        group.MapPost("/{id:guid}/activate", Activate);
        group.MapPatch("/{id:guid}/limit", UpdateLimit);
        group.MapGet("/{id:guid}/payroll-runs", ListPayrollRuns);
        group.MapPost("/{id:guid}/payroll-runs/{runId:guid}/reverse", ReversePayroll);

        return routes;
    }

    private static async Task<IResult> List(MiniPayrollDbContext db, CancellationToken cancellationToken)
    {
        var companies = await db.Companies
            .AsNoTracking()
            .Include(c => c.Subscription)
            .ThenInclude(s => s!.Plan)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        var adminIds = await CompanyAdminLookup.CompanyIdsWithAdminAsync(db, cancellationToken);

        var items = companies.Select(c => new CompanyListItem(
            c.Id,
            c.Name,
            c.ContactEmail,
            c.Subscription?.Status.ToString() ?? SubscriptionStatus.Pending.ToString(),
            c.Subscription?.EmployeeLimit ?? 0,
            c.IsSetupComplete,
            c.ActivatedAt,
            adminIds.Contains(c.Id)));

        return Results.Ok(items);
    }

    private static async Task<IResult> Get(Guid id, MiniPayrollDbContext db, CancellationToken cancellationToken)
    {
        var company = await db.Companies
            .AsNoTracking()
            .Include(c => c.Subscription)
            .ThenInclude(s => s!.Plan)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (company is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(await ToDetailAsync(company, db, cancellationToken));
    }

    private static async Task<IResult> Create(
        CreateCompanyRequest request,
        MiniPayrollDbContext db,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var plan = await db.Plans.SingleAsync(p => p.Name == PlatformLimits.DefaultPlanName, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            ContactEmail = request.ContactEmail.Trim(),
            ContactPhone = string.IsNullOrWhiteSpace(request.ContactPhone) ? null : request.ContactPhone.Trim(),
            CreatedAt = now
        };

        if (!EmployeeLimitRules.IsValid(request.EmployeeLimit))
        {
            return Results.BadRequest(new { error = EmployeeLimitRules.InvalidMessage });
        }

        company.Subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            PlanId = plan.Id,
            Status = SubscriptionStatus.Pending,
            EmployeeLimit = request.EmployeeLimit,
            GracePeriodDays = PlatformLimits.DefaultGracePeriodDays
        };

        db.Companies.Add(company);
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            ActorUserId = GetUserId(principal),
            Action = "company.create",
            Details = company.Name,
            OccurredAt = now
        });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/companies/{company.Id}", await ToDetailAsync(company, db, cancellationToken));
    }

    private static async Task<IResult> CreateAdmin(
        Guid id,
        CreateAdminRequest request,
        CompanyAdminService admins,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var result = await admins.CreateAsync(
            id,
            request.Email,
            GetUserId(principal),
            request.TemporaryPassword,
            cancellationToken);

        if (result.Status == CompanyAdminCreateStatus.Created && result.Response is { } created)
        {
            return Results.Ok(new CreateAdminResponse(created.UserId, created.Email));
        }

        return result.Status switch
        {
            CompanyAdminCreateStatus.CompanyNotFound => Results.NotFound(),
            CompanyAdminCreateStatus.AlreadyHasAdmin => Results.Conflict(
                new { error = "This company already has a Company Admin." }),
            CompanyAdminCreateStatus.EmailTaken => Results.Conflict(
                new { error = "A user with this email already exists." }),
            CompanyAdminCreateStatus.PasswordRequired => Results.BadRequest(
                new { error = "Temporary password is required." }),
            CompanyAdminCreateStatus.IdentityFailed => Results.BadRequest(new { errors = result.Errors }),
            _ => Results.BadRequest(new { error = "Could not create Company Admin." })
        };
    }

    private static async Task<IResult> Activate(
        Guid id,
        MiniPayrollDbContext db,
        UserManager<ApplicationUser> users,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var company = await db.Companies
            .Include(c => c.Subscription)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (company is null)
        {
            return Results.NotFound();
        }

        var hasAdmin = await users.Users.AnyAsync(u => u.CompanyId == id, cancellationToken);
        if (!hasAdmin)
        {
            return Results.BadRequest(new { error = "Create a Company Admin before activating the company." });
        }

        if (company.Subscription is null)
        {
            return Results.BadRequest(new { error = "Company has no subscription." });
        }

        var now = DateTimeOffset.UtcNow;
        company.Subscription.Status = SubscriptionStatus.Active;
        company.Subscription.CurrentPeriodStart = now;
        company.Subscription.CurrentPeriodEnd = new DateTimeOffset(
            now.Year,
            now.Month,
            DateTime.DaysInMonth(now.Year, now.Month),
            23, 59, 59, now.Offset);
        company.ActivatedAt = now;

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            ActorUserId = GetUserId(principal),
            Action = "company.activate",
            OccurredAt = now
        });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await ToDetailAsync(company, db, cancellationToken));
    }

    private static async Task<IResult> UpdateLimit(
        Guid id,
        UpdateLimitRequest request,
        MiniPayrollDbContext db,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        if (!EmployeeLimitRules.IsValid(request.EmployeeLimit))
        {
            return Results.BadRequest(new { error = EmployeeLimitRules.InvalidMessage });
        }

        var company = await db.Companies
            .Include(c => c.Subscription)
            .ThenInclude(s => s!.Plan)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (company is null)
        {
            return Results.NotFound();
        }

        if (company.Subscription is null)
        {
            return Results.BadRequest(new { error = "Company has no subscription." });
        }

        company.Subscription.EmployeeLimit = request.EmployeeLimit;
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            ActorUserId = GetUserId(principal),
            Action = "company.limit.change",
            Details = request.EmployeeLimit.ToString(),
            OccurredAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await ToDetailAsync(company, db, cancellationToken));
    }

    private static async Task<CompanyDetail> ToDetailAsync(
        Company company,
        MiniPayrollDbContext db,
        CancellationToken cancellationToken)
    {
        var admin = await CompanyAdminLookup.GetAsync(db, company.Id, cancellationToken);
        return new CompanyDetail(
            company.Id,
            company.Name,
            company.ContactEmail,
            company.ContactPhone,
            company.Subscription?.Status.ToString() ?? SubscriptionStatus.Pending.ToString(),
            company.Subscription?.EmployeeLimit ?? 0,
            company.Subscription?.Plan?.Name ?? PlatformLimits.DefaultPlanName,
            company.IsSetupComplete,
            company.ActivatedAt,
            admin.HasAdmin,
            admin.Email);
    }

    private static async Task<IResult> ListPayrollRuns(
        Guid id,
        [FromServices] PayrollCalculationService payroll,
        CancellationToken cancellationToken)
    {
        var result = await payroll.ListCompanyRunsAsync(id, cancellationToken);
        return result.Status == PayrollRunStatusCode.Success
            ? Results.Ok(result.Runs)
            : PayrollError(result.Status);
    }

    private static async Task<IResult> ReversePayroll(
        Guid id,
        Guid runId,
        ReversePayrollRequest? request,
        [FromServices] PayrollCalculationService payroll,
        CancellationToken cancellationToken)
    {
        var result = await payroll.ReverseAsync(id, runId, request?.Reason, cancellationToken);
        return result.Status == PayrollRunStatusCode.Success
            ? Results.Ok(result.Run)
            : PayrollError(result.Status);
    }

    private static IResult PayrollError(PayrollRunStatusCode status) =>
        Results.Json(new { error = status switch
        {
            PayrollRunStatusCode.InvalidInput => "A reversal reason is required.",
            PayrollRunStatusCode.NotFound => "The payroll run was not found.",
            PayrollRunStatusCode.Forbidden => "You are not allowed to reverse payroll.",
            PayrollRunStatusCode.RunLocked => "Only a finalized payroll run can be reversed.",
            PayrollRunStatusCode.CompanyNotFound => "The company was not found.",
            _ => "The payroll request could not be completed."
        }}, statusCode: PayrollHttpStatus.For(status));

    private static Guid GetUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    public sealed record CreateCompanyRequest(
        [Required, MinLength(2), MaxLength(200)] string Name,
        [Required, EmailAddress] string ContactEmail,
        string? ContactPhone,
        int EmployeeLimit);

    public sealed record UpdateLimitRequest(int EmployeeLimit);

    public sealed record CreateAdminRequest(
        [Required, EmailAddress] string Email,
        [Required] string TemporaryPassword);

    public sealed record CreateAdminResponse(Guid UserId, string Email);

    public sealed record ReversePayrollRequest(
        [Required, MinLength(1), MaxLength(500)] string Reason);

    public sealed record CompanyListItem(
        Guid Id,
        string Name,
        string ContactEmail,
        string Status,
        int EmployeeLimit,
        bool IsSetupComplete,
        DateTimeOffset? ActivatedAt,
        bool HasAdmin);

    public sealed record CompanyDetail(
        Guid Id,
        string Name,
        string ContactEmail,
        string? ContactPhone,
        string Status,
        int EmployeeLimit,
        string PlanName,
        bool IsSetupComplete,
        DateTimeOffset? ActivatedAt,
        bool HasAdmin,
        string? AdminEmail);
}
