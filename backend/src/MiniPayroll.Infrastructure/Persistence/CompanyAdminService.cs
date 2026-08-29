using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Infrastructure.Identity;

namespace MiniPayroll.Infrastructure.Persistence;

public enum CompanyAdminCreateStatus
{
    Created,
    CompanyNotFound,
    AlreadyHasAdmin,
    EmailTaken,
    IdentityFailed
}

public sealed record CompanyAdminCreated(Guid UserId, string Email, string TemporaryPassword);

public sealed record CompanyAdminCreateResult(
    CompanyAdminCreateStatus Status,
    CompanyAdminCreated? Response,
    IReadOnlyList<string> Errors);

public sealed class CompanyAdminService(
    MiniPayrollDbContext db,
    UserManager<ApplicationUser> users,
    RoleManager<ApplicationRole> roles)
{
    public async Task<CompanyAdminCreateResult> CreateAsync(
        Guid companyId,
        string email,
        Guid actorUserId,
        string? temporaryPassword = null,
        CancellationToken cancellationToken = default)
    {
        var companyExists = await db.Companies.AnyAsync(c => c.Id == companyId, cancellationToken);
        if (!companyExists)
        {
            return new CompanyAdminCreateResult(CompanyAdminCreateStatus.CompanyNotFound, null, []);
        }

        if (await users.Users.AnyAsync(u => u.CompanyId == companyId, cancellationToken))
        {
            return new CompanyAdminCreateResult(CompanyAdminCreateStatus.AlreadyHasAdmin, null, []);
        }

        var trimmed = email.Trim();
        if (await users.FindByEmailAsync(trimmed) is not null)
        {
            return new CompanyAdminCreateResult(CompanyAdminCreateStatus.EmailTaken, null, []);
        }

        if (!await roles.RoleExistsAsync(RoleNames.CompanyAdmin))
        {
            await roles.CreateAsync(new ApplicationRole(RoleNames.CompanyAdmin));
        }

        var password = temporaryPassword ?? GenerateTemporaryPassword();
        var admin = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = trimmed,
            Email = trimmed,
            EmailConfirmed = true,
            CompanyId = companyId,
            MustChangePassword = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var created = await users.CreateAsync(admin, password);
        if (!created.Succeeded)
        {
            return new CompanyAdminCreateResult(
                CompanyAdminCreateStatus.IdentityFailed,
                null,
                created.Errors.Select(e => e.Description).ToList());
        }

        await users.AddToRoleAsync(admin, RoleNames.CompanyAdmin);

        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            ActorUserId = actorUserId,
            Action = "company.admin.create",
            Details = trimmed,
            OccurredAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);

        return new CompanyAdminCreateResult(
            CompanyAdminCreateStatus.Created,
            new CompanyAdminCreated(admin.Id, trimmed, password),
            []);
    }

    private static string GenerateTemporaryPassword() =>
        $"Tmp_{Guid.NewGuid():N}"[..14] + "Aa1!";
}
