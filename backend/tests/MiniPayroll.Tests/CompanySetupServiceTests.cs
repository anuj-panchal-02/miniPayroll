using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public class CompanySetupServiceTests
{
    [Fact]
    public async Task Details_update_persists_partial_setup_and_advances_to_payroll()
    {
        var (db, service, _) = await CreateServiceAsync();
        await using var ownedDb = db;

        var result = await service.UpdateDetailsAsync(new CompanyDetailsInput(
            "  ABC Traders  ",
            " owner@example.com ",
            " 1234567890 ",
            " Main Road ",
            " ",
            " Pune ",
            " Maharashtra ",
            " 411001 "));

        Assert.Equal(CompanySetupStatus.Success, result.Status);
        Assert.Equal(CompanySetupStep.PayrollSettings, result.State!.SetupStep);

        db.ChangeTracker.Clear();
        var persisted = await db.Companies.SingleAsync();
        Assert.Equal("ABC Traders", persisted.Name);
        Assert.Equal("owner@example.com", persisted.ContactEmail);
        Assert.Null(persisted.AddressLine2);
        Assert.False(persisted.IsSetupComplete);
    }

    [Fact]
    public async Task Setup_state_resumes_from_persisted_values()
    {
        var (db, service, tenant) = await CreateServiceAsync(company =>
        {
            MakeValid(company);
            company.SetupStep = CompanySetupStep.Review;
        });
        await using var ownedDb = db;

        var result = await service.GetAsync();

        Assert.Equal(CompanySetupStatus.Success, result.Status);
        Assert.Equal(tenant.CompanyId, result.State!.CompanyId);
        Assert.Equal(CompanySetupStep.Review, result.State!.SetupStep);
        Assert.Collection(
            result.State.WeeklyOffDays,
            day => Assert.Equal("Saturday", day),
            day => Assert.Equal("Sunday", day));
        Assert.Equal("uploads/companies/logo.png", result.State.LogoPath);
    }

    [Fact]
    public async Task Missing_tenant_company_returns_not_found()
    {
        var database = UniqueDatabase();
        var otherCompanyId = Guid.NewGuid();
        await SeedAsync(database, NewCompany(otherCompanyId));

        var tenant = NewTenant(Guid.NewGuid());
        await using var db = TestDb.Create(tenant, database);
        var service = new CompanySetupService(db, tenant);

        var read = await service.GetAsync();
        var update = await service.UpdatePayrollSettingsAsync(
            new PayrollSettingsInput(DailyRateMethod.CalendarDays, 26, ["Sunday"]));

        Assert.Equal(CompanySetupStatus.CompanyNotFound, read.Status);
        Assert.Equal(CompanySetupStatus.CompanyNotFound, update.Status);
    }

    [Fact]
    public async Task Unscoped_context_returns_not_found_for_reads_writes_and_completion()
    {
        var database = UniqueDatabase();
        await SeedAsync(database, NewCompany(Guid.NewGuid()), NewCompany(Guid.NewGuid()));
        var unscoped = new StaticTenantContext
        {
            UserId = Guid.NewGuid(),
            IsSuperadmin = true
        };
        await using var db = TestDb.Create(unscoped, database);
        var service = new CompanySetupService(db, unscoped);

        var read = await service.GetAsync();
        var write = await service.UpdateDetailsAsync(ValidDetails());
        var complete = await service.CompleteAsync();

        Assert.Equal(CompanySetupStatus.CompanyNotFound, read.Status);
        Assert.Equal(CompanySetupStatus.CompanyNotFound, write.Status);
        Assert.Equal(CompanySetupStatus.CompanyNotFound, complete.Status);
        Assert.Null(read.State);
    }

    [Fact]
    public async Task Tenant_updates_cannot_modify_another_company()
    {
        var database = UniqueDatabase();
        var companyA = NewCompany(Guid.NewGuid());
        var companyB = NewCompany(Guid.NewGuid());
        companyB.Name = "Other Company";
        await SeedAsync(database, companyA, companyB);

        var tenant = NewTenant(companyA.Id);
        await using (var db = TestDb.Create(tenant, database))
        {
            var service = new CompanySetupService(db, tenant);
            var result = await service.UpdateDetailsAsync(ValidDetails("Tenant A"));
            Assert.Equal(CompanySetupStatus.Success, result.Status);
        }

        await using var verification = TestDb.Create(NullTenantContext.Instance, database);
        Assert.Equal("Tenant A", (await verification.Companies.FindAsync(companyA.Id))!.Name);
        Assert.Equal("Other Company", (await verification.Companies.FindAsync(companyB.Id))!.Name);
    }

    [Fact]
    public async Task Revisiting_details_and_payroll_never_regresses_progress()
    {
        var (db, service, _) = await CreateServiceAsync(company =>
        {
            MakeValid(company);
            company.SetupStep = CompanySetupStep.Review;
        });
        await using var ownedDb = db;

        var details = await service.UpdateDetailsAsync(ValidDetails("Updated Name"));
        var payroll = await service.UpdatePayrollSettingsAsync(
            new PayrollSettingsInput(DailyRateMethod.FixedThirty, 30, ["Sunday"]));

        Assert.Equal(CompanySetupStep.Review, details.State!.SetupStep);
        Assert.Equal(CompanySetupStep.Review, payroll.State!.SetupStep);
        var persisted = await db.Companies.SingleAsync();
        Assert.Equal(CompanySetupStep.Review, persisted.SetupStep);
        Assert.Equal(DailyRateMethod.FixedThirty, persisted.DailyRateMethod);
        Assert.Equal(30, persisted.WorkingDaysPerMonth);
        Assert.Equal("Sunday", persisted.WeeklyOffDays);
    }

    [Fact]
    public async Task Payroll_settings_cannot_skip_company_details()
    {
        var (db, service, _) = await CreateServiceAsync();
        await using var ownedDb = db;

        var result = await service.UpdatePayrollSettingsAsync(
            new PayrollSettingsInput(DailyRateMethod.FixedThirty, 30, ["Saturday", "Sunday"]));

        Assert.Equal(CompanySetupStatus.InvalidStep, result.Status);
        db.ChangeTracker.Clear();
        var persisted = await db.Companies.SingleAsync();
        Assert.Equal(CompanySetupStep.CompanyDetails, persisted.SetupStep);
        Assert.Equal(DailyRateMethod.CalendarDays, persisted.DailyRateMethod);
        Assert.Equal(26, persisted.WorkingDaysPerMonth);
        Assert.Equal("Sunday", persisted.WeeklyOffDays);
    }

    [Fact]
    public async Task Invalid_details_do_not_partially_persist()
    {
        var (db, service, _) = await CreateServiceAsync();
        await using var ownedDb = db;
        var originalName = (await db.Companies.SingleAsync()).Name;

        var result = await service.UpdateDetailsAsync(
            ValidDetails("Changed") with { ContactEmail = "not-an-email" });

        Assert.Equal(CompanySetupStatus.InvalidInput, result.Status);
        db.ChangeTracker.Clear();
        var persisted = await db.Companies.SingleAsync();
        Assert.Equal(originalName, persisted.Name);
        Assert.Equal(CompanySetupStep.CompanyDetails, persisted.SetupStep);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public async Task Invalid_payroll_settings_do_not_persist(int workingDays)
    {
        var (db, service, _) = await CreateServiceAsync(
            company => company.SetupStep = CompanySetupStep.PayrollSettings);
        await using var ownedDb = db;

        var result = await service.UpdatePayrollSettingsAsync(
            new PayrollSettingsInput(DailyRateMethod.FixedThirty, workingDays, ["Saturday", "Sunday"]));

        Assert.Equal(CompanySetupStatus.InvalidInput, result.Status);
        db.ChangeTracker.Clear();
        var persisted = await db.Companies.SingleAsync();
        Assert.Equal(26, persisted.WorkingDaysPerMonth);
        Assert.Equal(DailyRateMethod.CalendarDays, persisted.DailyRateMethod);
    }

    [Fact]
    public async Task Invalid_logo_path_does_not_persist()
    {
        var (db, service, _) = await CreateServiceAsync();
        await using var ownedDb = db;

        var result = await service.SaveLogoPathAsync(" ");

        Assert.Equal(CompanySetupStatus.InvalidInput, result.Status);
        Assert.Null((await db.Companies.SingleAsync()).LogoPath);
    }

    [Fact]
    public async Task Valid_logo_path_is_trimmed_and_persisted()
    {
        var (db, service, _) = await CreateServiceAsync();
        await using var ownedDb = db;

        var result = await service.SaveLogoPathAsync(" uploads/companies/logo.png ");

        Assert.Equal(CompanySetupStatus.Success, result.Status);
        Assert.Equal("uploads/companies/logo.png", result.State!.LogoPath);
        db.ChangeTracker.Clear();
        Assert.Equal(
            "uploads/companies/logo.png",
            (await db.Companies.SingleAsync()).LogoPath);
    }

    [Fact]
    public async Task Completion_cannot_skip_required_setup_steps()
    {
        var (db, service, _) = await CreateServiceAsync();
        await using var ownedDb = db;

        var result = await service.CompleteAsync();

        Assert.Equal(CompanySetupStatus.InvalidStep, result.Status);
        var persisted = await db.Companies.SingleAsync();
        Assert.False(persisted.IsSetupComplete);
        Assert.Equal(CompanySetupStep.CompanyDetails, persisted.SetupStep);
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task Completion_rejects_valid_data_before_review_step()
    {
        var (db, service, _) = await CreateServiceAsync(company =>
        {
            MakeValid(company);
            company.SetupStep = CompanySetupStep.PayrollSettings;
        });
        await using var ownedDb = db;

        var result = await service.CompleteAsync();

        Assert.Equal(CompanySetupStatus.InvalidStep, result.Status);
        var persisted = await db.Companies.SingleAsync();
        Assert.False(persisted.IsSetupComplete);
        Assert.Equal(CompanySetupStep.PayrollSettings, persisted.SetupStep);
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public Task Completion_reloads_and_rejects_persisted_detail_corruption() =>
        AssertPersistedCorruptionRejectedAsync(company => company.ContactPhone = null);

    [Fact]
    public Task Completion_reloads_and_rejects_persisted_payroll_corruption() =>
        AssertPersistedCorruptionRejectedAsync(company => company.WorkingDaysPerMonth = 0);

    [Fact]
    public Task Completion_reloads_and_rejects_persisted_logo_corruption() =>
        AssertPersistedCorruptionRejectedAsync(company => company.LogoPath = null);

    [Fact]
    public async Task Completion_discards_pending_tracked_changes_before_validation()
    {
        var (db, service, _) = await CreateServiceAsync(company =>
        {
            MakeValid(company);
            company.ContactPhone = null;
        });
        await using var ownedDb = db;
        var tracked = await db.Companies.SingleAsync();
        tracked.ContactPhone = "1234567890";

        var result = await service.CompleteAsync();

        Assert.Equal(CompanySetupStatus.InvalidInput, result.Status);
        db.ChangeTracker.Clear();
        var persisted = await db.Companies.SingleAsync();
        Assert.Null(persisted.ContactPhone);
        Assert.False(persisted.IsSetupComplete);
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task Completion_sets_complete_and_writes_tenant_audit()
    {
        var (db, service, tenant) = await CreateServiceAsync(MakeValid);
        await using var ownedDb = db;

        var result = await service.CompleteAsync();

        Assert.Equal(CompanySetupStatus.Success, result.Status);
        Assert.True(result.State!.IsSetupComplete);
        Assert.Equal(CompanySetupStep.Complete, result.State.SetupStep);

        var audit = await db.AuditLogs.SingleAsync();
        Assert.Equal("company.setup.complete", audit.Action);
        Assert.Equal(tenant.CompanyId, audit.CompanyId);
        Assert.Equal(tenant.UserId, audit.ActorUserId);
    }

    [Fact]
    public async Task Repeated_completion_returns_already_complete_without_duplicate_audit()
    {
        var (db, service, _) = await CreateServiceAsync(MakeValid);
        await using var ownedDb = db;
        Assert.Equal(CompanySetupStatus.Success, (await service.CompleteAsync()).Status);

        var repeated = await service.CompleteAsync();

        Assert.Equal(CompanySetupStatus.AlreadyComplete, repeated.Status);
        Assert.Single(await db.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task Writes_after_completion_return_already_complete()
    {
        var (db, service, _) = await CreateServiceAsync(company =>
        {
            MakeValid(company);
            company.IsSetupComplete = true;
            company.SetupStep = CompanySetupStep.Complete;
        });
        await using var ownedDb = db;

        var result = await service.SaveLogoPathAsync("uploads/replacement.png");

        Assert.Equal(CompanySetupStatus.AlreadyComplete, result.Status);
        Assert.Equal("uploads/companies/logo.png", (await db.Companies.SingleAsync()).LogoPath);
    }

    private static async Task<(MiniPayrollDbContext Db, CompanySetupService Service, StaticTenantContext Tenant)>
        CreateServiceAsync(Action<Company>? configure = null)
    {
        var database = UniqueDatabase();
        var company = NewCompany(Guid.NewGuid());
        configure?.Invoke(company);
        await SeedAsync(database, company);

        var tenant = NewTenant(company.Id);
        var db = TestDb.Create(tenant, database);
        return (db, new CompanySetupService(db, tenant), tenant);
    }

    private static async Task SeedAsync(string database, params Company[] companies)
    {
        await using var setup = TestDb.Create(NullTenantContext.Instance, database);
        setup.Companies.AddRange(companies);
        await setup.SaveChangesAsync();
    }

    private static async Task AssertPersistedCorruptionRejectedAsync(Action<Company> corrupt)
    {
        var database = UniqueDatabase();
        var company = NewCompany(Guid.NewGuid());
        MakeValid(company);
        await SeedAsync(database, company);
        var tenant = NewTenant(company.Id);

        await using var db = TestDb.Create(tenant, database);
        var service = new CompanySetupService(db, tenant);
        Assert.Equal(CompanySetupStatus.Success, (await service.GetAsync()).Status);

        await using (var corruption = TestDb.Create(NullTenantContext.Instance, database))
        {
            var persisted = await corruption.Companies.SingleAsync(c => c.Id == company.Id);
            corrupt(persisted);
            await corruption.SaveChangesAsync();
        }

        var result = await service.CompleteAsync();

        Assert.Equal(CompanySetupStatus.InvalidInput, result.Status);
        db.ChangeTracker.Clear();
        var reloaded = await db.Companies.SingleAsync();
        Assert.False(reloaded.IsSetupComplete);
        Assert.Empty(await db.AuditLogs.ToListAsync());
    }

    private static StaticTenantContext NewTenant(Guid companyId) => new()
    {
        UserId = Guid.NewGuid(),
        CompanyId = companyId,
        IsSuperadmin = false
    };

    private static Company NewCompany(Guid id) => new()
    {
        Id = id,
        Name = "Draft Company",
        ContactEmail = "draft@example.com",
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static CompanyDetailsInput ValidDetails(string name = "ABC Traders") => new(
        name,
        "owner@example.com",
        "1234567890",
        "Main Road",
        null,
        "Pune",
        "Maharashtra",
        "411001");

    private static void MakeValid(Company company)
    {
        company.Name = "ABC Traders";
        company.ContactEmail = "owner@example.com";
        company.ContactPhone = "1234567890";
        company.AddressLine1 = "Main Road";
        company.City = "Pune";
        company.State = "Maharashtra";
        company.PostalCode = "411001";
        company.LogoPath = "uploads/companies/logo.png";
        company.WorkingDaysPerMonth = 26;
        company.WeeklyOffDays = "Saturday,Sunday";
        company.DailyRateMethod = DailyRateMethod.CalendarDays;
        company.SetupStep = CompanySetupStep.Review;
    }

    private static string UniqueDatabase() => $"company-setup-{Guid.NewGuid():N}";
}
