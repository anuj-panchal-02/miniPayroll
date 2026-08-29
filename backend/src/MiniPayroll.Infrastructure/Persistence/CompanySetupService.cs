using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Auth;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Tenancy;

namespace MiniPayroll.Infrastructure.Persistence;

public enum CompanySetupStatus
{
    Success,
    CompanyNotFound,
    InvalidInput,
    InvalidStep,
    AlreadyComplete,
    Conflict
}

public sealed record CompanyDetailsInput(
    string? Name,
    string? ContactEmail,
    string? ContactPhone,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode);

public sealed record PayrollSettingsInput(
    DailyRateMethod DailyRateMethod,
    int WorkingDaysPerMonth,
    IReadOnlyCollection<string?>? WeeklyOffDays);

public sealed record CompanySetupState(
    Guid CompanyId,
    string Name,
    string ContactEmail,
    string? ContactPhone,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode,
    string? LogoPath,
    DailyRateMethod DailyRateMethod,
    int WorkingDaysPerMonth,
    IReadOnlyList<string> WeeklyOffDays,
    CompanySetupStep SetupStep,
    bool IsSetupComplete);

public sealed record CompanySetupResult(
    CompanySetupStatus Status,
    CompanySetupState? State);

public sealed class CompanySetupService(
    MiniPayrollDbContext db,
    ITenantContext tenant)
{
    public async Task<CompanySetupResult> GetAsync(
        CancellationToken cancellationToken = default)
    {
        var company = await GetTenantCompanyAsync(cancellationToken);
        return company is null
            ? Result(CompanySetupStatus.CompanyNotFound)
            : Result(CompanySetupStatus.Success, company);
    }

    public async Task<CompanySetupResult> UpdateDetailsAsync(
        CompanyDetailsInput? input,
        CancellationToken cancellationToken = default)
    {
        var company = await GetTenantCompanyAsync(cancellationToken);
        if (company is null)
        {
            return Result(CompanySetupStatus.CompanyNotFound);
        }

        if (company.IsSetupComplete)
        {
            return Result(CompanySetupStatus.AlreadyComplete, company);
        }

        if (input is null)
        {
            return Result(CompanySetupStatus.InvalidInput, company);
        }

        var candidate = new Company
        {
            Name = input.Name ?? string.Empty,
            ContactEmail = input.ContactEmail ?? string.Empty,
            ContactPhone = input.ContactPhone,
            AddressLine1 = input.AddressLine1,
            AddressLine2 = input.AddressLine2,
            City = input.City,
            State = input.State,
            PostalCode = input.PostalCode
        };
        CompanySetupRules.NormalizeCompanyDetails(candidate);

        if (!CompanySetupRules.HasValidCompanyDetails(candidate))
        {
            return Result(CompanySetupStatus.InvalidInput, company);
        }

        company.Name = candidate.Name;
        company.ContactEmail = candidate.ContactEmail;
        company.ContactPhone = candidate.ContactPhone;
        company.AddressLine1 = candidate.AddressLine1;
        company.AddressLine2 = candidate.AddressLine2;
        company.City = candidate.City;
        company.State = candidate.State;
        company.PostalCode = candidate.PostalCode;
        AdvanceTo(company, CompanySetupStep.PayrollSettings);

        await db.SaveChangesAsync(cancellationToken);
        return Result(CompanySetupStatus.Success, company);
    }

    public async Task<CompanySetupResult> UpdatePayrollSettingsAsync(
        PayrollSettingsInput? input,
        CancellationToken cancellationToken = default)
    {
        var company = await GetTenantCompanyAsync(cancellationToken);
        if (company is null)
        {
            return Result(CompanySetupStatus.CompanyNotFound);
        }

        if (company.IsSetupComplete)
        {
            return Result(CompanySetupStatus.AlreadyComplete, company);
        }

        if (company.SetupStep < CompanySetupStep.PayrollSettings)
        {
            return Result(CompanySetupStatus.InvalidStep, company);
        }

        if (input is null
            || !CompanySetupRules.TryCanonicalizeWeeklyOffDays(
                input.WeeklyOffDays,
                out var canonicalDays))
        {
            return Result(CompanySetupStatus.InvalidInput, company);
        }

        var candidate = new Company
        {
            DailyRateMethod = input.DailyRateMethod,
            WorkingDaysPerMonth = input.WorkingDaysPerMonth,
            WeeklyOffDays = canonicalDays
        };
        if (!CompanySetupRules.HasValidPayrollSettings(candidate))
        {
            return Result(CompanySetupStatus.InvalidInput, company);
        }

        company.DailyRateMethod = candidate.DailyRateMethod;
        company.WorkingDaysPerMonth = candidate.WorkingDaysPerMonth;
        company.WeeklyOffDays = candidate.WeeklyOffDays;
        AdvanceTo(company, CompanySetupStep.Review);

        await db.SaveChangesAsync(cancellationToken);
        return Result(CompanySetupStatus.Success, company);
    }

    public async Task<CompanySetupResult> SaveLogoPathAsync(
        string? logoPath,
        CancellationToken cancellationToken = default)
    {
        var company = await GetTenantCompanyAsync(cancellationToken);
        if (company is null)
        {
            return Result(CompanySetupStatus.CompanyNotFound);
        }

        if (company.IsSetupComplete)
        {
            return Result(CompanySetupStatus.AlreadyComplete, company);
        }

        var normalizedPath = logoPath?.Trim();
        if (string.IsNullOrEmpty(normalizedPath)
            || normalizedPath.Length > CompanySetupRules.LogoPathMaxLength)
        {
            return Result(CompanySetupStatus.InvalidInput, company);
        }

        company.LogoPath = normalizedPath;
        await db.SaveChangesAsync(cancellationToken);
        return Result(CompanySetupStatus.Success, company);
    }

    public async Task<CompanySetupResult> CompleteAsync(
        CancellationToken cancellationToken = default)
    {
        var company = await GetTenantCompanyAsync(cancellationToken);
        if (company is null)
        {
            return Result(CompanySetupStatus.CompanyNotFound);
        }

        await db.Entry(company).ReloadAsync(cancellationToken);

        if (company.IsSetupComplete)
        {
            return Result(CompanySetupStatus.AlreadyComplete, company);
        }

        if (company.SetupStep < CompanySetupStep.Review)
        {
            return Result(CompanySetupStatus.InvalidStep, company);
        }

        if (!CompanySetupRules.CanComplete(company))
        {
            return Result(CompanySetupStatus.InvalidInput, company);
        }

        company.IsSetupComplete = true;
        company.SetupStep = CompanySetupStep.Complete;
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            ActorUserId = tenant.UserId,
            Action = AuditActions.CompanySetupComplete,
            OccurredAt = DateTimeOffset.UtcNow
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return await ResolveCompletionRaceAsync(cancellationToken);
        }

        return Result(CompanySetupStatus.Success, company);
    }

    private async Task<CompanySetupResult> ResolveCompletionRaceAsync(
        CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        var persisted = await GetTenantCompanyAsync(cancellationToken);
        if (persisted is null)
        {
            return Result(CompanySetupStatus.CompanyNotFound);
        }

        return persisted.IsSetupComplete
            ? Result(CompanySetupStatus.AlreadyComplete, persisted)
            : Result(CompanySetupStatus.Conflict, persisted);
    }

    private Task<Company?> GetTenantCompanyAsync(CancellationToken cancellationToken)
    {
        if (tenant.IsSuperadmin || tenant.CompanyId is not { } companyId)
        {
            return Task.FromResult<Company?>(null);
        }

        return db.Companies.SingleOrDefaultAsync(
            company => company.Id == companyId,
            cancellationToken);
    }

    private static void AdvanceTo(Company company, CompanySetupStep step)
    {
        if (company.SetupStep < step)
        {
            company.SetupStep = step;
        }
    }

    private static CompanySetupResult Result(
        CompanySetupStatus status,
        Company? company = null) =>
        new(status, company is null ? null : ToState(company));

    private static CompanySetupState ToState(Company company) => new(
        company.Id,
        company.Name,
        company.ContactEmail,
        company.ContactPhone,
        company.AddressLine1,
        company.AddressLine2,
        company.City,
        company.State,
        company.PostalCode,
        company.LogoPath,
        company.DailyRateMethod,
        company.WorkingDaysPerMonth,
        company.WeeklyOffDays.Split(',', StringSplitOptions.RemoveEmptyEntries),
        company.SetupStep,
        company.IsSetupComplete);
}
