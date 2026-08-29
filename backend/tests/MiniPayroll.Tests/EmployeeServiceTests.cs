using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public class EmployeeServiceTests
{
    [Fact]
    public async Task Create_persists_normalized_employee_and_masks_account()
    {
        var (db, service, _) = await CreateServiceAsync();
        await using var owned = db;

        var result = await service.CreateAsync(ValidInput(" EMP-01 ", " 1234 5678 9012 "));

        Assert.Equal(EmployeeStatusCode.Success, result.Status);
        Assert.Equal("EMP-01", result.Employee!.EmployeeCode);
        Assert.Equal("123456789012", result.Employee.BankAccountNumber);
        Assert.Equal("****9012", result.Employee.MaskedAccountNumber);
        Assert.Equal(EmployeeStatus.Active, result.Employee.Status);
        Assert.Contains(
            await db.AuditLogs.ToListAsync(),
            audit => audit.Action == AuditActions.EmployeeCreate);
    }

    [Fact]
    public async Task List_omits_full_account_and_ifsc()
    {
        var (db, service, _) = await CreateServiceAsync();
        await using var owned = db;
        await service.CreateAsync(ValidInput("EMP-01"));

        var list = await service.ListAsync();

        Assert.Equal(EmployeeStatusCode.Success, list.Status);
        var item = Assert.Single(list.List!.Employees);
        Assert.Equal("****9012", item.MaskedAccountNumber);
        Assert.Equal(1, list.List.ActiveCount);
        Assert.Equal(9, list.List.EmployeeLimit);
    }

    [Fact]
    public async Task Duplicate_employee_code_is_rejected()
    {
        var (db, service, _) = await CreateServiceAsync();
        await using var owned = db;
        Assert.Equal(EmployeeStatusCode.Success, (await service.CreateAsync(ValidInput("EMP-01"))).Status);

        var duplicate = await service.CreateAsync(ValidInput("EMP-01"));

        Assert.Equal(EmployeeStatusCode.DuplicateEmployeeCode, duplicate.Status);
        Assert.Equal(1, await db.Employees.CountAsync());
    }

    [Fact]
    public async Task Active_limit_blocks_create_but_inactive_does_not_consume_a_seat()
    {
        var (db, service, _) = await CreateServiceAsync(limit: 1);
        await using var owned = db;
        var created = await service.CreateAsync(ValidInput("EMP-01"));
        Assert.Equal(EmployeeStatusCode.Success, created.Status);

        var blocked = await service.CreateAsync(ValidInput("EMP-02"));
        Assert.Equal(EmployeeStatusCode.EmployeeLimitReached, blocked.Status);
        Assert.Equal(1, blocked.EmployeeLimit);

        var deactivated = await service.UpdateAsync(created.Employee!.Id, ValidInput("EMP-01", status: EmployeeStatus.Inactive));
        Assert.Equal(EmployeeStatusCode.Success, deactivated.Status);
        Assert.Equal(AuditActions.EmployeeDeactivate, (await db.AuditLogs.OrderBy(log => log.OccurredAt).LastAsync()).Action);

        var allowed = await service.CreateAsync(ValidInput("EMP-02"));
        Assert.Equal(EmployeeStatusCode.Success, allowed.Status);
    }

    [Fact]
    public async Task Exit_date_does_not_auto_deactivate()
    {
        var (db, service, _) = await CreateServiceAsync();
        await using var owned = db;
        var created = await service.CreateAsync(ValidInput("EMP-01"));

        var updated = await service.UpdateAsync(
            created.Employee!.Id,
            ValidInput("EMP-01", exitDate: new DateOnly(2026, 8, 31)));

        Assert.Equal(EmployeeStatus.Active, updated.Employee!.Status);
        Assert.Equal(new DateOnly(2026, 8, 31), updated.Employee.ExitDate);
    }

    [Fact]
    public async Task Incomplete_setup_cannot_list_or_create()
    {
        var (db, service, _) = await CreateServiceAsync(setupComplete: false);
        await using var owned = db;

        Assert.Equal(EmployeeStatusCode.SetupIncomplete, (await service.ListAsync()).Status);
        Assert.Equal(EmployeeStatusCode.SetupIncomplete, (await service.CreateAsync(ValidInput("EMP-01"))).Status);
    }

    [Fact]
    public async Task Suspended_company_can_read_but_not_write()
    {
        var (db, service, _) = await CreateServiceAsync(status: SubscriptionStatus.Suspended);
        await using var owned = db;

        Assert.Equal(EmployeeStatusCode.Success, (await service.ListAsync()).Status);
        Assert.Equal(EmployeeStatusCode.SubscriptionReadOnly, (await service.CreateAsync(ValidInput("EMP-01"))).Status);
    }

    [Fact]
    public async Task Tenant_cannot_read_or_update_another_company_employee()
    {
        var database = UniqueDatabase();
        var companyA = await SeedCompanyAsync(database);
        var companyB = await SeedCompanyAsync(database);
        Guid foreignId;
        await using (var writer = TestDb.Create(NewTenant(companyB.Id), database))
        {
            var created = await new EmployeeService(writer, NewTenant(companyB.Id))
                .CreateAsync(ValidInput("B-1"));
            foreignId = created.Employee!.Id;
        }

        await using var db = TestDb.Create(NewTenant(companyA.Id), database);
        var service = new EmployeeService(db, NewTenant(companyA.Id));
        Assert.Equal(EmployeeStatusCode.NotFound, (await service.GetAsync(foreignId)).Status);
        Assert.Equal(EmployeeStatusCode.NotFound, (await service.UpdateAsync(foreignId, ValidInput("HACK"))).Status);
    }

    [Fact]
    public async Task Unscoped_context_returns_not_found()
    {
        var database = UniqueDatabase();
        await SeedCompanyAsync(database);
        var unscoped = new StaticTenantContext { UserId = Guid.NewGuid(), IsSuperadmin = true };
        await using var db = TestDb.Create(unscoped, database);
        var service = new EmployeeService(db, unscoped);

        Assert.Equal(EmployeeStatusCode.CompanyNotFound, (await service.ListAsync()).Status);
        Assert.Equal(EmployeeStatusCode.CompanyNotFound, (await service.CreateAsync(ValidInput("EMP-01"))).Status);
    }

    [Fact]
    public async Task Invalid_input_is_not_persisted()
    {
        var (db, service, _) = await CreateServiceAsync();
        await using var owned = db;

        var result = await service.CreateAsync(ValidInput("BAD CODE"));

        Assert.Equal(EmployeeStatusCode.InvalidInput, result.Status);
        Assert.Empty(await db.Employees.ToListAsync());
    }

    [Fact]
    public async Task Draft_create_does_not_consume_an_active_seat()
    {
        var (db, service, _) = await CreateServiceAsync(limit: 1);
        await using var owned = db;
        Assert.Equal(EmployeeStatusCode.Success, (await service.CreateAsync(ValidInput("EMP-01"))).Status);

        var draft = await service.CreateAsync(DraftInput("EMP-02"));

        Assert.Equal(EmployeeStatusCode.Success, draft.Status);
        Assert.Equal(EmployeeStatus.Draft, draft.Employee!.Status);
        Assert.Equal(1, draft.Employee.DraftStep);
        var list = await service.ListAsync();
        Assert.Equal(1, list.List!.ActiveCount);
        Assert.Equal(2, list.List.Employees.Count);
    }

    [Fact]
    public async Task Completing_a_draft_at_the_active_cap_is_blocked()
    {
        var (db, service, _) = await CreateServiceAsync(limit: 1);
        await using var owned = db;
        Assert.Equal(EmployeeStatusCode.Success, (await service.CreateAsync(ValidInput("EMP-01"))).Status);
        var draft = await service.CreateAsync(DraftInput("EMP-02"));

        var completed = await service.UpdateAsync(draft.Employee!.Id, ValidInput("EMP-02"));

        Assert.Equal(EmployeeStatusCode.EmployeeLimitReached, completed.Status);
        Assert.Equal(EmployeeStatus.Draft, (await db.Employees.SingleAsync(item => item.EmployeeCode == "EMP-02")).Status);
    }

    [Fact]
    public async Task Completing_a_draft_becomes_active()
    {
        var (db, service, _) = await CreateServiceAsync();
        await using var owned = db;
        var draft = await service.CreateAsync(DraftInput("EMP-01"));

        var completed = await service.UpdateAsync(draft.Employee!.Id, ValidInput("EMP-01"));

        Assert.Equal(EmployeeStatusCode.Success, completed.Status);
        Assert.Equal(EmployeeStatus.Active, completed.Employee!.Status);
        Assert.Null(completed.Employee.DraftStep);
        Assert.Equal(1, (await service.ListAsync()).List!.ActiveCount);
    }

    [Fact]
    public async Task Draft_create_can_resume_on_the_salary_step()
    {
        var (db, service, _) = await CreateServiceAsync();
        await using var owned = db;

        var draft = await service.CreateAsync(DraftInput("EMP-03", draftStep: 4));

        Assert.Equal(EmployeeStatusCode.Success, draft.Status);
        Assert.Equal(EmployeeStatus.Draft, draft.Employee!.Status);
        Assert.Equal(4, draft.Employee.DraftStep);
    }

    [Fact]
    public async Task Duplicate_draft_code_is_rejected()
    {
        var (db, service, _) = await CreateServiceAsync();
        await using var owned = db;
        Assert.Equal(EmployeeStatusCode.Success, (await service.CreateAsync(DraftInput("EMP-01"))).Status);

        var duplicate = await service.CreateAsync(DraftInput("EMP-01"));

        Assert.Equal(EmployeeStatusCode.DuplicateEmployeeCode, duplicate.Status);
    }

    [Fact]
    public async Task Active_employee_cannot_be_saved_as_a_draft()
    {
        var (db, service, _) = await CreateServiceAsync();
        await using var owned = db;
        var created = await service.CreateAsync(ValidInput("EMP-01"));

        var demoted = await service.UpdateAsync(created.Employee!.Id, DraftInput("EMP-01"));

        Assert.Equal(EmployeeStatusCode.InvalidInput, demoted.Status);
        Assert.Equal(EmployeeStatus.Active, (await db.Employees.SingleAsync()).Status);
    }

    [Fact]
    public async Task Suspended_company_cannot_save_a_draft()
    {
        var (db, service, _) = await CreateServiceAsync(status: SubscriptionStatus.Suspended);
        await using var owned = db;

        Assert.Equal(
            EmployeeStatusCode.SubscriptionReadOnly,
            (await service.CreateAsync(DraftInput("EMP-01"))).Status);
    }

    private static async Task<(MiniPayrollDbContext Db, EmployeeService Service, StaticTenantContext Tenant)>
        CreateServiceAsync(
            bool setupComplete = true,
            SubscriptionStatus status = SubscriptionStatus.Active,
            int limit = 9)
    {
        var database = UniqueDatabase();
        var company = await SeedCompanyAsync(database, setupComplete, status, limit);
        var tenant = NewTenant(company.Id);
        var db = TestDb.Create(tenant, database);
        return (db, new EmployeeService(db, tenant), tenant);
    }

    private static async Task<Company> SeedCompanyAsync(
        string database,
        bool setupComplete = true,
        SubscriptionStatus status = SubscriptionStatus.Active,
        int limit = 9)
    {
        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = "Acme",
            ContactEmail = "acme@example.com",
            IsSetupComplete = setupComplete,
            SetupStep = setupComplete ? CompanySetupStep.Complete : CompanySetupStep.CompanyDetails,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var plan = new Plan
        {
            Id = Guid.NewGuid(),
            Name = $"Basic-{Guid.NewGuid():N}",
            PricePerEmployee = 49m,
            DefaultEmployeeLimit = 9
        };
        company.Subscription = new Subscription
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            PlanId = plan.Id,
            Status = status,
            EmployeeLimit = limit,
            GracePeriodDays = 7
        };

        await using var setup = TestDb.Create(NullTenantContext.Instance, database);
        setup.Plans.Add(plan);
        setup.Companies.Add(company);
        await setup.SaveChangesAsync();
        return company;
    }

    private static StaticTenantContext NewTenant(Guid companyId) => new()
    {
        UserId = Guid.NewGuid(),
        CompanyId = companyId,
        IsSuperadmin = false
    };

    private static EmployeeInput ValidInput(
        string code,
        string account = "123456789012",
        EmployeeStatus? status = null,
        DateOnly? exitDate = null) => new(
        code,
        "Ada Lovelace",
        null,
        "9876543210",
        "ada@example.com",
        "Main Road",
        null,
        "Pune",
        "Maharashtra",
        "411001",
        "Engineer",
        null,
        new DateOnly(2026, 1, 15),
        exitDate,
        status,
        "HDFC Bank",
        account,
        "HDFC0001234",
        null,
        null,
        SalaryStructure: BasicSalary());

    private static SalaryStructureInput BasicSalary(DateOnly? effectiveFrom = null) => new(
        effectiveFrom ?? new DateOnly(2026, 1, 15),
        [new SalaryStructureComponentInput(
            "Basic Salary",
            SalaryComponentType.Earning,
            SalaryComponentValueType.FixedAmount,
            25000m,
            0)]);

    private static EmployeeInput DraftInput(string code, int draftStep = 1) => new(
        code,
        "Ada Lovelace",
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        true,
        draftStep);

    private static string UniqueDatabase() => $"employees-{Guid.NewGuid():N}";
}
