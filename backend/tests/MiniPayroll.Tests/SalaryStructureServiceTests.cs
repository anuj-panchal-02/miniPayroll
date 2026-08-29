using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public sealed class SalaryStructureServiceTests
{
    [Fact]
    public async Task Structure_requires_exactly_one_fixed_basic_salary()
    {
        var (db, salaries, employee) = await CreateServiceAsync();
        await using var owned = db;

        var result = await salaries.CreateAsync(employee.Id, new SalaryStructureInput(
            new DateOnly(2026, 1, 1),
            [new SalaryStructureComponentInput(
                "HRA", SalaryComponentType.Earning, SalaryComponentValueType.FixedAmount, 5000m, 0)]));

        Assert.Equal(SalaryStructureStatusCode.InvalidInput, result.Status);
        Assert.Empty(await db.SalaryStructures.ToListAsync());
    }

    [Fact]
    public async Task Latest_version_effective_on_requested_date_is_returned()
    {
        var (db, salaries, employee) = await CreateServiceAsync();
        await using var owned = db;

        Assert.Equal(SalaryStructureStatusCode.Success,
            (await salaries.CreateAsync(employee.Id, Input(new DateOnly(2026, 1, 1), 20000m))).Status);
        Assert.Equal(SalaryStructureStatusCode.Success,
            (await salaries.CreateAsync(employee.Id, Input(new DateOnly(2026, 3, 1), 25000m))).Status);

        var january = await salaries.GetEffectiveAsync(employee.Id, new DateOnly(2026, 2, 28));
        var march = await salaries.GetEffectiveAsync(employee.Id, new DateOnly(2026, 3, 31));

        Assert.Equal(20000m, january.Structure!.RecurringEarnings);
        Assert.Equal(25000m, march.Structure!.RecurringEarnings);
    }

    [Fact]
    public async Task Tenant_cannot_read_another_companys_salary_structures()
    {
        var database = $"salary-structures-{Guid.NewGuid():N}";
        var companyA = await SeedCompanyAsync(database);
        var companyB = await SeedCompanyAsync(database);
        var foreignEmployee = await SeedEmployeeAsync(database, companyB.Id);

        await using var db = TestDb.Create(NewTenant(companyA.Id), database);
        var result = await new SalaryStructureService(db, NewTenant(companyA.Id))
            .ListAsync(foreignEmployee.Id);

        Assert.Equal(SalaryStructureStatusCode.EmployeeNotFound, result.Status);
    }

    private static SalaryStructureInput Input(DateOnly effectiveFrom, decimal basic) => new(
        effectiveFrom,
        [new SalaryStructureComponentInput(
            "Basic Salary", SalaryComponentType.Earning, SalaryComponentValueType.FixedAmount, basic, 0)]);

    private static async Task<(MiniPayrollDbContext Db, SalaryStructureService Salaries, Employee Employee)>
        CreateServiceAsync()
    {
        var database = $"salary-structures-{Guid.NewGuid():N}";
        var company = await SeedCompanyAsync(database);
        var employee = await SeedEmployeeAsync(database, company.Id);
        var tenant = NewTenant(company.Id);
        var db = TestDb.Create(tenant, database);
        return (db, new SalaryStructureService(db, tenant), employee);
    }

    private static async Task<Company> SeedCompanyAsync(string database)
    {
        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = "Acme",
            ContactEmail = $"{Guid.NewGuid():N}@example.com",
            IsSetupComplete = true,
            SetupStep = CompanySetupStep.Complete,
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
            Status = SubscriptionStatus.Active,
            EmployeeLimit = 9,
            GracePeriodDays = 7
        };

        await using var db = TestDb.Create(NullTenantContext.Instance, database);
        db.Plans.Add(plan);
        db.Companies.Add(company);
        await db.SaveChangesAsync();
        return company;
    }

    private static async Task<Employee> SeedEmployeeAsync(string database, Guid companyId)
    {
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            EmployeeCode = $"EMP-{Guid.NewGuid():N}",
            FullName = "Ada Lovelace",
            Phone = "9876543210",
            Email = "ada@example.com",
            AddressLine1 = "Main Road",
            City = "Pune",
            State = "Maharashtra",
            PostalCode = "411001",
            Designation = "Engineer",
            JoiningDate = new DateOnly(2026, 1, 1),
            BankName = "HDFC Bank",
            BankAccountNumber = "123456789012",
            Ifsc = "HDFC0001234",
            Status = EmployeeStatus.Active,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await using var db = TestDb.Create(NullTenantContext.Instance, database);
        db.Employees.Add(employee);
        await db.SaveChangesAsync();
        return employee;
    }

    private static StaticTenantContext NewTenant(Guid companyId) => new()
    {
        UserId = Guid.NewGuid(),
        CompanyId = companyId,
        IsSuperadmin = false
    };
}
