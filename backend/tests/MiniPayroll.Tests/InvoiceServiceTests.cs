using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Billing;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Subscriptions;
using MiniPayroll.Domain.Tenancy;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Tests;

public sealed class InvoiceServiceTests
{
    private static readonly DateTimeOffset Jan2026 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset EndJan2026 = new(2026, 1, 31, 23, 59, 59, TimeSpan.Zero);
    private static readonly DateTimeOffset Jan2027 = new(2027, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset EndJan2027 = new(2027, 1, 31, 23, 59, 59, TimeSpan.Zero);

    [Fact]
    public async Task Create_persists_invoice_lines_and_zero_tax_totals()
    {
        var fixture = await SeedAsync();
        var result = await fixture.Service.CreateAsync(fixture.CompanyId, Jan2026, EndJan2026, 3);

        Assert.Equal(InvoiceCommandStatus.Success, result.Status);
        var invoice = result.Invoice!;
        var line = Assert.Single(invoice.Lines);
        Assert.Equal(3m, line.Quantity);
        Assert.Equal(499m, line.UnitPrice);
        Assert.Equal(1497m, line.Amount);
        Assert.Equal(1497m, invoice.Subtotal);
        Assert.Equal(0m, invoice.Tax);
        Assert.Equal(1497m, invoice.Total);
        Assert.Equal("INR", invoice.Currency);
        Assert.Equal(InvoiceStatus.Draft.ToString(), invoice.Status);
        Assert.Null(invoice.ExternalInvoiceId);
        Assert.Contains(await ReloadAuditsAsync(fixture), item => item == AuditActions.InvoiceCreate);
    }

    [Fact]
    public async Task Historical_invoice_keeps_the_2026_price_after_a_2027_revision()
    {
        var fixture = await SeedAsync(futurePrice: 699m);
        var created = await fixture.Service.CreateAsync(fixture.CompanyId, Jan2026, EndJan2026, 2);
        Assert.Equal(998m, created.Invoice!.Total);

        await using var reader = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        var stored = await reader.Invoices.Include(item => item.Lines).SingleAsync();
        Assert.Equal(499m, Assert.Single(stored.Lines).UnitPrice);
        Assert.Equal(998m, stored.Total);

        var later = NewService(fixture.Database, new FrozenTimeProvider(Jan2027));
        var next = await later.CreateAsync(fixture.CompanyId, Jan2027, EndJan2027, 2);
        Assert.Equal(699m, Assert.Single(next.Invoice!.Lines).UnitPrice);
        Assert.Equal(1398m, next.Invoice.Total);
        Assert.Equal(998m, stored.Total);
    }

    [Fact]
    public async Task Duplicate_period_is_rejected_until_the_open_invoice_is_voided()
    {
        var fixture = await SeedAsync();
        var first = await fixture.Service.CreateAsync(fixture.CompanyId, Jan2026, EndJan2026, 1);
        Assert.Equal(InvoiceCommandStatus.Success, first.Status);

        var duplicate = await fixture.Service.CreateAsync(fixture.CompanyId, Jan2026, EndJan2026, 1);
        Assert.Equal(InvoiceCommandStatus.DuplicatePeriod, duplicate.Status);

        Assert.Equal(
            InvoiceCommandStatus.Success,
            (await fixture.Service.VoidAsync(fixture.CompanyId, first.Invoice!.Id)).Status);

        var reissued = await fixture.Service.CreateAsync(fixture.CompanyId, Jan2026, EndJan2026, 1);
        Assert.Equal(InvoiceCommandStatus.Success, reissued.Status);
        Assert.NotEqual(first.Invoice.Id, reissued.Invoice!.Id);
    }

    [Fact]
    public async Task Issue_assigns_a_unique_invoice_number()
    {
        var fixture = await SeedAsync();
        var first = await fixture.Service.CreateAsync(fixture.CompanyId, Jan2026, EndJan2026, 1);
        var secondPeriodStart = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
        var secondPeriodEnd = new DateTimeOffset(2026, 2, 28, 23, 59, 59, TimeSpan.Zero);
        var second = await fixture.Service.CreateAsync(fixture.CompanyId, secondPeriodStart, secondPeriodEnd, 1);

        var issuedFirst = await fixture.Service.IssueAsync(fixture.CompanyId, first.Invoice!.Id);
        var issuedSecond = await fixture.Service.IssueAsync(fixture.CompanyId, second.Invoice!.Id);

        Assert.Equal("INV-2026-000001", issuedFirst.Invoice!.InvoiceNumber);
        Assert.Equal("INV-2026-000002", issuedSecond.Invoice!.InvoiceNumber);
        Assert.Equal(InvoiceStatus.Issued.ToString(), issuedFirst.Invoice.Status);
        Assert.Equal(EndJan2026, issuedFirst.Invoice.DueAt);
    }

    [Fact]
    public async Task Apply_payment_reaches_partial_then_paid_and_rejects_overpay()
    {
        var fixture = await SeedAsync();
        var created = await fixture.Service.CreateAsync(fixture.CompanyId, Jan2026, EndJan2026, 2);
        await fixture.Service.IssueAsync(fixture.CompanyId, created.Invoice!.Id);

        var partial = await fixture.Service.ApplyPaymentAsync(fixture.CompanyId, created.Invoice.Id, 400m);
        Assert.Equal(InvoiceStatus.PartiallyPaid.ToString(), partial.Invoice!.Status);
        Assert.Equal(400m, partial.Invoice.AmountPaid);

        Assert.Equal(
            InvoiceCommandStatus.Overpay,
            (await fixture.Service.ApplyPaymentAsync(fixture.CompanyId, created.Invoice.Id, 700m)).Status);

        var paid = await fixture.Service.ApplyPaymentAsync(fixture.CompanyId, created.Invoice.Id, 598m);
        Assert.Equal(InvoiceStatus.Paid.ToString(), paid.Invoice!.Status);
        Assert.Equal(998m, paid.Invoice.AmountPaid);
        Assert.NotNull(paid.Invoice.PaidAt);

        await using var db = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        var covering = Assert.Single(await db.Payments.ToListAsync());
        Assert.Equal("2026-01", covering.BillingPeriod);
        Assert.Equal(998m, covering.Amount);
    }

    [Fact]
    public async Task Company_admin_can_read_own_invoices_but_cannot_issue_or_void()
    {
        var fixture = await SeedAsync();
        var created = await fixture.Service.CreateAsync(fixture.CompanyId, Jan2026, EndJan2026, 1);

        var admin = new StaticTenantContext
        {
            UserId = Guid.NewGuid(),
            CompanyId = fixture.CompanyId,
            IsSuperadmin = false
        };
        await using var db = TestDb.Create(admin, fixture.Database);
        var own = new InvoiceService(db, admin, fixture.Clock);

        var listed = await own.ListOwnAsync();
        Assert.Equal(InvoiceCommandStatus.Success, listed.Status);
        Assert.Single(listed.Invoices!);

        Assert.Equal(
            InvoiceCommandStatus.Forbidden,
            (await own.IssueAsync(fixture.CompanyId, created.Invoice!.Id)).Status);
        Assert.Equal(
            InvoiceCommandStatus.Forbidden,
            (await own.VoidAsync(fixture.CompanyId, created.Invoice.Id)).Status);
    }

    [Fact]
    public async Task Company_isolation_hides_foreign_invoices_and_lines()
    {
        var fixture = await SeedAsync();
        await fixture.Service.CreateAsync(fixture.CompanyId, Jan2026, EndJan2026, 1);
        var otherCompanyId = await SeedSecondCompanyAsync(fixture.Database);
        await NewService(fixture.Database, fixture.Clock)
            .CreateAsync(otherCompanyId, Jan2026, EndJan2026, 1);

        var tenantA = new StaticTenantContext
        {
            UserId = Guid.NewGuid(),
            CompanyId = fixture.CompanyId,
            IsSuperadmin = false
        };
        await using var db = TestDb.Create(tenantA, fixture.Database);
        var service = new InvoiceService(db, tenantA, fixture.Clock);

        Assert.Equal(InvoiceCommandStatus.Forbidden, (await service.ListAsync(otherCompanyId)).Status);
        Assert.Empty(await db.Invoices.Where(item => item.CompanyId == otherCompanyId).ToListAsync());
        Assert.Empty(await db.InvoiceLines.Where(item => item.CompanyId == otherCompanyId).ToListAsync());
        Assert.Equal(fixture.CompanyId, Assert.Single(await db.Invoices.ToListAsync()).CompanyId);
    }

    [Fact]
    public async Task Create_can_take_quantity_from_an_existing_snapshot()
    {
        var fixture = await SeedAsync(snapshotEmployees: 4);
        var result = await fixture.Service.CreateAsync(fixture.CompanyId, Jan2026, EndJan2026, null);
        Assert.Equal(InvoiceCommandStatus.Success, result.Status);
        Assert.Equal(4m, Assert.Single(result.Invoice!.Lines).Quantity);
        Assert.Equal(1996m, result.Invoice.Total);
    }

    private sealed record Fixture(
        string Database,
        Guid CompanyId,
        Guid PlanId,
        InvoiceService Service,
        FrozenTimeProvider Clock);

    private static async Task<Fixture> SeedAsync(
        string? database = null,
        int? snapshotEmployees = null,
        decimal? futurePrice = null)
    {
        database ??= $"invoice-{Guid.NewGuid():N}";
        var companyId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var subscriptionId = Guid.NewGuid();
        var plan = new Plan
        {
            Id = planId,
            Code = "starter",
            Name = "Starter",
            IsActive = true,
            MaxActiveEmployees = 50,
            PricePerEmployee = 499m,
            DefaultEmployeeLimit = 50
        };
        PlanPricing.Revise(plan, BillingCycle.Monthly, 499m, Jan2026);
        if (futurePrice is { } revised)
        {
            PlanPricing.Revise(plan, BillingCycle.Monthly, revised, Jan2027);
        }

        await using (var writer = TestDb.Create(NullTenantContext.Instance, database))
        {
            writer.Plans.Add(plan);
            writer.Companies.Add(new Company
            {
                Id = companyId,
                Name = "Invoice Co",
                ContactEmail = $"{companyId:N}@example.com",
                IsSetupComplete = true,
                SetupStep = CompanySetupStep.Complete,
                CreatedAt = Jan2026,
                ActivatedAt = Jan2026,
                Subscription = new Subscription
                {
                    Id = subscriptionId,
                    CompanyId = companyId,
                    PlanId = planId,
                    Status = SubscriptionStatus.Active,
                    BillingCycle = BillingCycle.Monthly,
                    EmployeeLimit = 10,
                    GracePeriodDays = 7,
                    CreatedAt = Jan2026,
                    UpdatedAt = Jan2026
                }
            });
            if (snapshotEmployees is { } seats)
            {
                writer.BillingPeriods.Add(new BillingPeriodSnapshot
                {
                    Id = Guid.NewGuid(),
                    CompanyId = companyId,
                    SubscriptionId = subscriptionId,
                    BillingPeriod = "2026-01",
                    PricePerEmployee = 499m,
                    BillableEmployees = seats,
                    BillableSource = BillableSource.ActiveHeadcount,
                    AmountDue = 499m * seats,
                    DueDate = EndJan2026
                });
            }

            await writer.SaveChangesAsync();
        }

        var clock = new FrozenTimeProvider(Jan2026);
        return new Fixture(database, companyId, planId, NewService(database, clock), clock);
    }

    private static async Task<IReadOnlyList<string>> ReloadAuditsAsync(Fixture fixture)
    {
        await using var db = TestDb.Create(NullTenantContext.Instance, fixture.Database);
        return await db.AuditLogs.IgnoreQueryFilters().Select(item => item.Action).ToListAsync();
    }

    private static async Task<Guid> SeedSecondCompanyAsync(string database)
    {
        var companyId = Guid.NewGuid();
        await using var writer = TestDb.Create(NullTenantContext.Instance, database);
        var planId = await writer.Plans.Select(item => item.Id).SingleAsync();
        writer.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Other Co",
            ContactEmail = $"{companyId:N}@example.com",
            CreatedAt = Jan2026,
            ActivatedAt = Jan2026,
            Subscription = new Subscription
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                PlanId = planId,
                Status = SubscriptionStatus.Active,
                BillingCycle = BillingCycle.Monthly,
                EmployeeLimit = 10,
                GracePeriodDays = 7,
                CreatedAt = Jan2026,
                UpdatedAt = Jan2026
            }
        });
        await writer.SaveChangesAsync();
        return companyId;
    }

    private static InvoiceService NewService(string database, TimeProvider clock)
    {
        var db = TestDb.Create(NullTenantContext.Instance, database);
        return new InvoiceService(db, NullTenantContext.Instance, clock);
    }

    private sealed class FrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
