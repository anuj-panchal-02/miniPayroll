using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public sealed class CompanySetupConcurrencyTests
{
    [Fact]
    public void Company_row_version_is_a_store_generated_concurrency_token()
    {
        using var db = TestDb.Create(NullTenantContext.Instance);

        var rowVersion = db.Model
            .FindEntityType(typeof(Company))!
            .FindProperty(nameof(Company.RowVersion))!;

        Assert.True(rowVersion.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, rowVersion.ValueGenerated);
    }

    [Fact]
    public void Setup_completion_audit_is_unique_per_company()
    {
        using var db = TestDb.Create(NullTenantContext.Instance);

        var index = SetupCompleteAuditIndex(db);

        Assert.True(index.IsUnique);
        Assert.Equal(
            $"[Action] = '{AuditActions.CompanySetupComplete}'",
            index.GetFilter());
    }

    [Fact]
    public void Other_audit_actions_stay_repeatable_for_a_company()
    {
        using var db = TestDb.Create(NullTenantContext.Instance);

        var index = SetupCompleteAuditIndex(db);

        Assert.Contains(AuditActions.CompanySetupComplete, index.GetFilter());
        Assert.All(
            db.Model.FindEntityType(typeof(AuditLog))!.GetIndexes().Where(i => i.IsUnique),
            unique => Assert.False(string.IsNullOrEmpty(unique.GetFilter())));
    }

    [Fact]
    public async Task Losing_a_completion_race_returns_already_complete_and_drops_its_audit()
    {
        var database = UniqueDatabase();
        var company = ValidCompany();
        await SeedAsync(database, company);
        var tenant = NewTenant(company.Id);
        var winner = new RacingInterceptor(database, company.Id, complete: true);

        await using var db = TestDb.Create(tenant, database, winner);
        var service = new CompanySetupService(db, tenant);

        var result = await service.CompleteAsync();

        Assert.True(winner.Raced);
        Assert.Equal(CompanySetupStatus.AlreadyComplete, result.Status);
        Assert.True(result.State!.IsSetupComplete);
        Assert.Equal(CompanySetupStep.Complete, result.State.SetupStep);
        Assert.Empty(db.ChangeTracker.Entries<AuditLog>());
    }

    [Fact]
    public async Task An_already_complete_company_is_never_audited_again()
    {
        var database = UniqueDatabase();
        var company = ValidCompany();
        company.IsSetupComplete = true;
        company.SetupStep = CompanySetupStep.Complete;
        await SeedAsync(database, company);
        var tenant = NewTenant(company.Id);

        await using var db = TestDb.Create(tenant, database);
        var service = new CompanySetupService(db, tenant);

        var first = await service.CompleteAsync();
        var second = await service.CompleteAsync();

        Assert.Equal(CompanySetupStatus.AlreadyComplete, first.Status);
        Assert.Equal(CompanySetupStatus.AlreadyComplete, second.Status);
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task A_concurrent_edit_that_did_not_complete_setup_returns_conflict()
    {
        var database = UniqueDatabase();
        var company = ValidCompany();
        await SeedAsync(database, company);
        var tenant = NewTenant(company.Id);
        var editor = new RacingInterceptor(database, company.Id, complete: false);

        await using var db = TestDb.Create(tenant, database, editor);
        var service = new CompanySetupService(db, tenant);

        var result = await service.CompleteAsync();

        Assert.True(editor.Raced);
        Assert.Equal(CompanySetupStatus.Conflict, result.Status);
        Assert.False(result.State!.IsSetupComplete);
        Assert.Empty(db.ChangeTracker.Entries<AuditLog>());

        await using var verification = TestDb.Create(NullTenantContext.Instance, database);
        var persisted = await verification.Companies.SingleAsync();
        Assert.False(persisted.IsSetupComplete);
        Assert.Equal("Renamed By Another Request", persisted.Name);
    }

    private static IIndex SetupCompleteAuditIndex(MiniPayrollDbContext db) =>
        db.Model
            .FindEntityType(typeof(AuditLog))!
            .GetIndexes()
            .Single(index => index.Properties
                .Select(property => property.Name)
                .SequenceEqual([nameof(AuditLog.CompanyId), nameof(AuditLog.Action)]));

    private static async Task SeedAsync(string database, Company company)
    {
        await using var setup = TestDb.Create(NullTenantContext.Instance, database);
        setup.Companies.Add(company);
        await TestLocations.SeedPuneMaharashtraAsync(setup);
        await setup.SaveChangesAsync();
    }

    private static StaticTenantContext NewTenant(Guid companyId) => new()
    {
        UserId = Guid.NewGuid(),
        CompanyId = companyId,
        IsSuperadmin = false
    };

    private static Company ValidCompany() => new()
    {
        Id = Guid.NewGuid(),
        Name = "ABC Traders",
        ContactEmail = "owner@example.com",
        ContactPhone = "1234567890",
        AddressLine1 = "Main Road",
        City = "Pune",
        State = "Maharashtra",
        PostalCode = "411001",
        LogoPath = "uploads/companies/logo.png",
        WorkingDaysPerMonth = 26,
        WeeklyOffDays = "Saturday,Sunday",
        DailyRateMethod = DailyRateMethod.CalendarDays,
        SetupStep = CompanySetupStep.Review,
        CreatedAt = DateTimeOffset.UtcNow,
        RowVersion = [1]
    };

    private static string UniqueDatabase() => $"company-setup-race-{Guid.NewGuid():N}";

    private sealed class RacingInterceptor(string database, Guid companyId, bool complete)
        : SaveChangesInterceptor
    {
        public bool Raced { get; private set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Raced)
            {
                return result;
            }

            Raced = true;
            await using var racing = TestDb.Create(NullTenantContext.Instance, database);
            var persisted = await racing.Companies
                .SingleAsync(company => company.Id == companyId, cancellationToken);
            persisted.RowVersion = [2];

            if (complete)
            {
                persisted.IsSetupComplete = true;
                persisted.SetupStep = CompanySetupStep.Complete;
                racing.AuditLogs.Add(new AuditLog
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    ActorUserId = Guid.NewGuid(),
                    Action = AuditActions.CompanySetupComplete,
                    OccurredAt = DateTimeOffset.UtcNow
                });
            }
            else
            {
                persisted.Name = "Renamed By Another Request";
            }

            await racing.SaveChangesAsync(cancellationToken);
            return result;
        }
    }
}
