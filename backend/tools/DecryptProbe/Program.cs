using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Persistence;

var connection = args.Length > 0
    ? args[0]
    : "Server=DESKTOP-R28DE7L\\SQLEXPRESS;Database=MiniPayroll;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
var keys = Path.GetFullPath(Path.Combine(
    args.Length > 1 ? args[1] : ".",
    "dataprotection-keys"));

var services = new ServiceCollection();
services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keys));
var sp = services.BuildServiceProvider();
var dp = sp.GetRequiredService<IDataProtectionProvider>();

var tenant = new StaticTenantContext { IsSuperadmin = true, UserId = Guid.NewGuid() };
var options = new DbContextOptionsBuilder<MiniPayrollDbContext>()
    .UseSqlServer(connection)
    .Options;
await using var db = new MiniPayrollDbContext(options, tenant, dp);

try
{
    var count = await db.Employees.IgnoreQueryFilters().CountAsync();
    Console.WriteLine($"employee-count={count}");
    var loaded = await db.Employees.IgnoreQueryFilters().AsNoTracking().Take(5).ToListAsync();
    Console.WriteLine($"loaded={loaded.Count}");
    foreach (var employee in loaded)
    {
        var accountLen = employee.BankAccountNumber?.Length ?? 0;
        var ifscLen = employee.Ifsc?.Length ?? 0;
        Console.WriteLine($"ok code={employee.EmployeeCode} accountLen={accountLen} ifscLen={ifscLen}");
    }

    var billing = new BillingService(db, tenant);
    var companyId = await db.Companies.IgnoreQueryFilters().Select(c => c.Id).FirstAsync();
    var result = await billing.GetAsync(companyId);
    Console.WriteLine($"billing={result.Status} periods={result.Billing?.Periods.Count}");
}
catch (Exception ex)
{
    Console.WriteLine($"FAIL {ex.GetType().Name}: {ex.Message}");
    if (ex.InnerException is not null)
    {
        Console.WriteLine($"INNER {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
    }
}
