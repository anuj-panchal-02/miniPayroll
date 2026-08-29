using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Identity;

namespace MiniPayroll.Infrastructure.Persistence;

public class MiniPayrollDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    private readonly ITenantContext _tenant;
    private readonly IDataProtectionProvider _dataProtection;

    public MiniPayrollDbContext(
        DbContextOptions<MiniPayrollDbContext> options,
        ITenantContext? tenantContext = null,
        IDataProtectionProvider? dataProtection = null)
        : base(options)
    {
        _tenant = tenantContext ?? NullTenantContext.Instance;
        _dataProtection = dataProtection ?? new EphemeralDataProtectionProvider();
    }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<SalaryComponent> SalaryComponents => Set<SalaryComponent>();
    public DbSet<SalaryStructure> SalaryStructures => Set<SalaryStructure>();
    public DbSet<EmployeeSalaryComponent> EmployeeSalaryComponents => Set<EmployeeSalaryComponent>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>().ToTable(TableNames.User);
        builder.Entity<ApplicationRole>().ToTable(TableNames.Role);
        builder.Entity<IdentityUserRole<Guid>>().ToTable(TableNames.UserRole);
        builder.Entity<IdentityUserClaim<Guid>>().ToTable(TableNames.UserClaim);
        builder.Entity<IdentityRoleClaim<Guid>>().ToTable(TableNames.RoleClaim);
        builder.Entity<IdentityUserLogin<Guid>>().ToTable(TableNames.UserLogin);
        builder.Entity<IdentityUserToken<Guid>>().ToTable(TableNames.UserToken);

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(u => u.MustChangePassword).IsRequired();
            entity.HasOne(u => u.Company)
                .WithMany()
                .HasForeignKey(u => u.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Company>(entity =>
        {
            entity.ToTable(TableNames.Company);
            entity.Property(c => c.Name).HasMaxLength(200).IsRequired();
            entity.Property(c => c.ContactEmail).HasMaxLength(256).IsRequired();
            entity.Property(c => c.ContactPhone).HasMaxLength(30);
            entity.Property(c => c.AddressLine1).HasMaxLength(200);
            entity.Property(c => c.AddressLine2).HasMaxLength(200);
            entity.Property(c => c.City).HasMaxLength(100);
            entity.Property(c => c.State).HasMaxLength(100);
            entity.Property(c => c.PostalCode).HasMaxLength(20);
            entity.Property(c => c.LogoPath).HasMaxLength(500);
            entity.Property(c => c.SetupStep)
                .HasDefaultValue(CompanySetupStep.CompanyDetails)
                .IsRequired();
            entity.Property(c => c.WeeklyOffDays).HasMaxLength(100).IsRequired();
            entity.Property(c => c.RowVersion).IsRowVersion();
            entity.HasIndex(c => c.ContactEmail);
            entity.HasQueryFilter(c => _tenant.IsSuperadmin || c.Id == _tenant.CompanyId);
        });

        builder.Entity<Plan>(entity =>
        {
            entity.ToTable(TableNames.Plan);
            entity.Property(p => p.Name).HasMaxLength(100).IsRequired();
            entity.Property(p => p.PricePerEmployee).HasColumnType("decimal(18,2)");
            entity.HasIndex(p => p.Name).IsUnique();
        });

        builder.Entity<Subscription>(entity =>
        {
            entity.ToTable(TableNames.Subscription);
            entity.HasIndex(s => s.CompanyId).IsUnique();
            entity.HasOne(s => s.Company)
                .WithOne(c => c.Subscription)
                .HasForeignKey<Subscription>(s => s.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(s => s.Plan)
                .WithMany(p => p.Subscriptions)
                .HasForeignKey(s => s.PlanId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(s => _tenant.IsSuperadmin || s.CompanyId == _tenant.CompanyId);
        });

        builder.Entity<Payment>(entity =>
        {
            entity.ToTable(TableNames.Payment);
            entity.Property(p => p.Amount).HasColumnType("decimal(18,2)");
            entity.Property(p => p.PaymentMode).HasMaxLength(50).IsRequired();
            entity.Property(p => p.InvoiceGstReference).HasMaxLength(100);
            entity.Property(p => p.BillingPeriod).HasMaxLength(7).IsRequired();
            entity.HasOne(p => p.Company)
                .WithMany()
                .HasForeignKey(p => p.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(p => p.Subscription)
                .WithMany(s => s.Payments)
                .HasForeignKey(p => p.SubscriptionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(p => _tenant.IsSuperadmin || p.CompanyId == _tenant.CompanyId);
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.ToTable(TableNames.AuditLog);
            entity.Property(a => a.Action).HasMaxLength(100).IsRequired();
            entity.Property(a => a.Reason).HasMaxLength(500);
            entity.Property(a => a.Details).HasMaxLength(4000);
            entity.HasIndex(a => new { a.CompanyId, a.Action })
                .IsUnique()
                .HasFilter($"[Action] = '{AuditActions.CompanySetupComplete}'");
            entity.HasQueryFilter(a =>
                _tenant.IsSuperadmin || (a.CompanyId.HasValue && a.CompanyId == _tenant.CompanyId));
        });

        var bankProtector = _dataProtection.CreateProtector(EncryptedStringConverter.Purpose);
        var encrypted = new EncryptedStringConverter(bankProtector);

        builder.Entity<Employee>(entity =>
        {
            entity.ToTable(TableNames.Employee);
            entity.Property(e => e.EmployeeCode).HasMaxLength(32).IsRequired();
            entity.Property(e => e.FullName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Phone).HasMaxLength(30).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.Property(e => e.AddressLine1).HasMaxLength(200).IsRequired();
            entity.Property(e => e.AddressLine2).HasMaxLength(200);
            entity.Property(e => e.City).HasMaxLength(100).IsRequired();
            entity.Property(e => e.State).HasMaxLength(100).IsRequired();
            entity.Property(e => e.PostalCode).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Designation).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Department).HasMaxLength(100);
            entity.Property(e => e.DraftStep);
            entity.Property(e => e.BankName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.BankAccountNumber)
                .HasConversion(encrypted)
                .HasMaxLength(512)
                .IsRequired();
            entity.Property(e => e.Ifsc)
                .HasConversion(encrypted)
                .HasMaxLength(512)
                .IsRequired();
            entity.Property(e => e.UpiId).HasMaxLength(100);
            entity.Property(e => e.OvertimeRate).HasColumnType("decimal(18,2)");
            entity.HasIndex(e => new { e.CompanyId, e.EmployeeCode }).IsUnique();
            entity.HasOne(e => e.Company)
                .WithMany()
                .HasForeignKey(e => e.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(e => _tenant.IsSuperadmin || e.CompanyId == _tenant.CompanyId);
        });

        builder.Entity<SalaryComponent>(entity =>
        {
            entity.ToTable(TableNames.SalaryComponent);
            entity.Property(component => component.Name).HasMaxLength(100).IsRequired();
            entity.HasIndex(component => new { component.CompanyId, component.Name, component.Type })
                .IsUnique();
            entity.HasOne(component => component.Company)
                .WithMany()
                .HasForeignKey(component => component.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(component =>
                _tenant.IsSuperadmin || component.CompanyId == _tenant.CompanyId);
        });

        builder.Entity<SalaryStructure>(entity =>
        {
            entity.ToTable(TableNames.SalaryStructure);
            entity.HasIndex(structure => new { structure.EmployeeId, structure.EffectiveFrom })
                .IsUnique();
            entity.HasIndex(structure => new { structure.CompanyId, structure.EmployeeId });
            entity.HasOne(structure => structure.Company)
                .WithMany()
                .HasForeignKey(structure => structure.CompanyId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(structure => structure.Employee)
                .WithMany()
                .HasForeignKey(structure => structure.EmployeeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(structure =>
                _tenant.IsSuperadmin || structure.CompanyId == _tenant.CompanyId);
        });

        builder.Entity<EmployeeSalaryComponent>(entity =>
        {
            entity.ToTable(TableNames.EmployeeSalaryComponent);
            entity.Property(component => component.Name).HasMaxLength(100).IsRequired();
            entity.Property(component => component.Value).HasColumnType("decimal(18,2)");
            entity.HasIndex(component => new { component.SalaryStructureId, component.SortOrder })
                .IsUnique();
            entity.HasOne(component => component.SalaryStructure)
                .WithMany(structure => structure.Components)
                .HasForeignKey(component => component.SalaryStructureId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(component => component.SalaryComponent)
                .WithMany()
                .HasForeignKey(component => component.SalaryComponentId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
