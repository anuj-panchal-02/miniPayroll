using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public class TableNamingTests
{
    [Fact]
    public void All_mapped_tables_use_the_mp_prefix()
    {
        using var db = TestDb.Create(NullTenantContext.Instance);

        var tables = db.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .Where(name => name is not null)
            .Cast<string>()
            .ToList();

        Assert.NotEmpty(tables);
        Assert.All(tables, name => Assert.StartsWith(TableNames.Prefix, name, StringComparison.Ordinal));
    }

    [Fact]
    public void Saas_layer_tables_match_the_prd_names()
    {
        using var db = TestDb.Create(NullTenantContext.Instance);
        var tables = db.Model.GetEntityTypes()
            .Select(entity => entity.GetTableName())
            .ToHashSet();

        Assert.Contains(TableNames.User, tables);
        Assert.Contains(TableNames.Role, tables);
        Assert.Contains(TableNames.Company, tables);
        Assert.Contains(TableNames.Plan, tables);
        Assert.Contains(TableNames.Subscription, tables);
        Assert.Contains(TableNames.Payment, tables);
        Assert.Contains(TableNames.AuditLog, tables);
        Assert.Contains(TableNames.Employee, tables);
        Assert.Contains(TableNames.SalaryComponent, tables);
        Assert.Contains(TableNames.SalaryStructure, tables);
        Assert.Contains(TableNames.EmployeeSalaryComponent, tables);
    }
}
