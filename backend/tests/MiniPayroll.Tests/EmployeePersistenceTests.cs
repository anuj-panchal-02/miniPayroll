using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public class EmployeePersistenceTests
{
    [Fact]
    public void Employee_table_has_unique_company_code_index_and_tenant_filter()
    {
        using var db = TestDb.Create(NullTenantContext.Instance);
        var entity = db.Model.FindEntityType(typeof(Employee));

        Assert.NotNull(entity);
        Assert.Equal(TableNames.Employee, entity!.GetTableName());
        Assert.Contains(
            entity.GetIndexes(),
            index => index.IsUnique
                && index.Properties.Select(property => property.Name)
                    .SequenceEqual([nameof(Employee.CompanyId), nameof(Employee.EmployeeCode)]));
        Assert.NotEmpty(entity.GetDeclaredQueryFilters());
    }

    [Fact]
    public void Encrypted_ifsc_ciphertext_exceeds_the_old_128_column()
    {
        var directory = Directory.CreateTempSubdirectory("mp-ifsc-");
        try
        {
            var protector = DataProtectionProvider.Create(directory)
                .CreateProtector(EncryptedStringConverter.Purpose);
            var ciphertext = protector.Protect("HDFC0001234");

            Assert.InRange(ciphertext.Length, 129, 512);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [Fact]
    public void Ifsc_column_is_wide_enough_for_data_protection_ciphertext()
    {
        using var db = TestDb.Create(NullTenantContext.Instance);
        var entity = db.Model.FindEntityType(typeof(Employee));
        var ifsc = entity!.FindProperty(nameof(Employee.Ifsc));

        Assert.Equal(512, ifsc!.GetMaxLength());
    }

    [Fact]
    public async Task Bank_fields_round_trip_through_the_data_protection_converter()
    {
        var database = $"employee-crypto-{Guid.NewGuid():N}";
        var company = NewCompany();
        var employee = NewEmployee(company.Id, "EMP-01", "123456789012", "HDFC0001234");

        await using (var write = TestDb.Create(NullTenantContext.Instance, database))
        {
            write.Companies.Add(company);
            write.Employees.Add(employee);
            await write.SaveChangesAsync();
        }

        await using var read = TestDb.Create(NullTenantContext.Instance, database);
        var persisted = await read.Employees.SingleAsync();
        Assert.Equal("123456789012", persisted.BankAccountNumber);
        Assert.Equal("HDFC0001234", persisted.Ifsc);
    }

    [Fact]
    public async Task Tenant_query_filter_hides_other_company_employees()
    {
        var database = $"employee-filter-{Guid.NewGuid():N}";
        var companyA = NewCompany();
        var companyB = NewCompany();
        await using (var seed = TestDb.Create(NullTenantContext.Instance, database))
        {
            seed.Companies.AddRange(companyA, companyB);
            seed.Employees.AddRange(
                NewEmployee(companyA.Id, "A-1"),
                NewEmployee(companyB.Id, "B-1"));
            await seed.SaveChangesAsync();
        }

        var tenant = new StaticTenantContext
        {
            UserId = Guid.NewGuid(),
            CompanyId = companyA.Id,
            IsSuperadmin = false
        };
        await using var db = TestDb.Create(tenant, database);
        var visible = await db.Employees.Select(employee => employee.EmployeeCode).ToListAsync();

        Assert.Equal(["A-1"], visible);
    }

    private static Company NewCompany() => new()
    {
        Id = Guid.NewGuid(),
        Name = "Acme",
        ContactEmail = "acme@example.com",
        CreatedAt = DateTimeOffset.UtcNow
    };

    private static Employee NewEmployee(
        Guid companyId,
        string code,
        string account = "123456789012",
        string ifsc = "HDFC0001234") => new()
    {
        Id = Guid.NewGuid(),
        CompanyId = companyId,
        EmployeeCode = code,
        FullName = "Ada Lovelace",
        Phone = "9876543210",
        Email = "ada@example.com",
        AddressLine1 = "Main Road",
        City = "Pune",
        State = "Maharashtra",
        PostalCode = "411001",
        Designation = "Engineer",
        EmploymentType = EmploymentType.FullTimeMonthly,
        JoiningDate = new DateOnly(2026, 1, 15),
        Status = EmployeeStatus.Active,
        BankName = "HDFC Bank",
        BankAccountNumber = account,
        Ifsc = ifsc,
        CreatedAt = DateTimeOffset.UtcNow
    };
}
