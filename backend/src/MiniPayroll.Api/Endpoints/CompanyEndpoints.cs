using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Auth;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Subscriptions;
using MiniPayroll.Infrastructure.Payments;
using MiniPayroll.Infrastructure.Persistence;

namespace MiniPayroll.Api.Endpoints;

public static class CompanyEndpoints
{
    public static IEndpointRouteBuilder MapCompanyEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/companies").RequireAuthorization(policy =>
            policy.RequireRole(RoleNames.Superadmin));

        group.MapGet("/", List);
        group.MapGet("/{id:guid}", Get);
        group.MapPost("/", Create);
        group.MapPost("/{id:guid}/admin", CreateAdmin);
        group.MapPost("/{id:guid}/activate", Activate);
        group.MapPost("/{id:guid}/subscription/past-due", MarkPastDue);
        group.MapPost("/{id:guid}/subscription/grace", EnterGrace);
        group.MapPost("/{id:guid}/subscription/suspend", Suspend);
        group.MapPost("/{id:guid}/subscription/cancel", Cancel);
        group.MapPost("/{id:guid}/subscription/expire", Expire);
        group.MapPost("/{id:guid}/subscription/reactivate", Reactivate);
        group.MapPost("/{id:guid}/subscription/plan", ChangePlan);
        group.MapPatch("/{id:guid}/limit", UpdateLimit);
        group.MapGet("/{id:guid}/payroll-runs", ListPayrollRuns);
        group.MapPost("/{id:guid}/payroll-runs/{runId:guid}/reverse", ReversePayroll);
        group.MapGet("/{id:guid}/billing", GetBilling);
        group.MapPost("/{id:guid}/billing/payment-links", CreatePaymentLink);
        group.MapPost("/{id:guid}/payments", RecordPayment);
        group.MapGet("/{id:guid}/invoices", ListInvoices);
        group.MapPost("/{id:guid}/invoices", CreateInvoice);
        group.MapGet("/{id:guid}/invoices/{invoiceId:guid}", GetInvoice);
        group.MapPost("/{id:guid}/invoices/{invoiceId:guid}/issue", IssueInvoice);
        group.MapPost("/{id:guid}/invoices/{invoiceId:guid}/payment-pending", MarkInvoicePaymentPending);
        group.MapPost("/{id:guid}/invoices/{invoiceId:guid}/payments", ApplyInvoicePayment);
        group.MapPost("/{id:guid}/invoices/{invoiceId:guid}/fail", FailInvoice);
        group.MapPost("/{id:guid}/invoices/{invoiceId:guid}/void", VoidInvoice);
        group.MapPost("/{id:guid}/invoices/{invoiceId:guid}/refund", RefundInvoice);

        return routes;
    }

    private static async Task<IResult> List(MiniPayrollDbContext db, CancellationToken cancellationToken)
    {
        var companies = await db.Companies
            .AsNoTracking()
            .Include(c => c.Subscription)
            .ThenInclude(s => s!.Plan)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        var adminIds = await CompanyAdminLookup.CompanyIdsWithAdminAsync(db, cancellationToken);

        var items = companies.Select(c => new CompanyListItem(
            c.Id,
            c.Name,
            c.ContactEmail,
            c.Subscription?.Status.ToString() ?? SubscriptionStatus.Trialing.ToString(),
            c.Subscription?.EmployeeLimit ?? 0,
            c.IsSetupComplete,
            c.ActivatedAt,
            adminIds.Contains(c.Id)));

        return Results.Ok(items);
    }

    private static async Task<IResult> Get(Guid id, MiniPayrollDbContext db, CancellationToken cancellationToken)
    {
        var company = await db.Companies
            .AsNoTracking()
            .Include(c => c.Subscription)
            .ThenInclude(s => s!.Plan)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (company is null)
        {
            return Results.NotFound();
        }

        return Results.Ok(await ToDetailAsync(company, db, cancellationToken));
    }

    private static async Task<IResult> Create(
        CreateCompanyRequest request,
        MiniPayrollDbContext db,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var plan = await db.Plans.SingleAsync(p => p.Code == PlatformLimits.DefaultPlanCode, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var company = new Company
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            ContactEmail = request.ContactEmail.Trim(),
            ContactPhone = string.IsNullOrWhiteSpace(request.ContactPhone) ? null : request.ContactPhone.Trim(),
            CreatedAt = now
        };

        if (!EmployeeLimitRules.IsValid(request.EmployeeLimit))
        {
            return Results.BadRequest(new { error = EmployeeLimitRules.InvalidMessage });
        }

        var created = SubscriptionLifecycle.Create(
            company.Id,
            plan,
            request.EmployeeLimit,
            BillingCycle.Monthly,
            now,
            []);
        if (created.Status != SubscriptionLifecycleStatus.Success || created.Subscription is null)
        {
            return Results.BadRequest(new { error = "Could not assign the default plan." });
        }

        company.Subscription = created.Subscription;
        db.Companies.Add(company);
        if (created.Events is { Count: > 0 })
        {
            db.SubscriptionEvents.AddRange(created.Events);
        }
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            ActorUserId = GetUserId(principal),
            Action = "company.create",
            Details = company.Name,
            OccurredAt = now
        });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/companies/{company.Id}", await ToDetailAsync(company, db, cancellationToken));
    }

    private static async Task<IResult> CreateAdmin(
        Guid id,
        CreateAdminRequest request,
        CompanyAdminService admins,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var result = await admins.CreateAsync(
            id,
            request.Email,
            GetUserId(principal),
            request.TemporaryPassword,
            cancellationToken);

        if (result.Status == CompanyAdminCreateStatus.Created && result.Response is { } created)
        {
            return Results.Ok(new CreateAdminResponse(created.UserId, created.Email));
        }

        return result.Status switch
        {
            CompanyAdminCreateStatus.CompanyNotFound => Results.NotFound(),
            CompanyAdminCreateStatus.AlreadyHasAdmin => Results.Conflict(
                new { error = "This company already has a Company Admin." }),
            CompanyAdminCreateStatus.EmailTaken => Results.Conflict(
                new { error = "A user with this email already exists." }),
            CompanyAdminCreateStatus.PasswordRequired => Results.BadRequest(
                new { error = "Temporary password is required." }),
            CompanyAdminCreateStatus.IdentityFailed => Results.BadRequest(new { errors = result.Errors }),
            _ => Results.BadRequest(new { error = "Could not create Company Admin." })
        };
    }

    private static Task<IResult> Activate(
        Guid id,
        SubscriptionLifecycleService lifecycle,
        MiniPayrollDbContext db,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken) =>
        RunCommandAsync(
            id,
            db,
            () => lifecycle.ActivateAsync(id, GetUserId(principal), requireAdmin: true, cancellationToken),
            cancellationToken);

    private static Task<IResult> MarkPastDue(
        Guid id,
        SubscriptionLifecycleService lifecycle,
        MiniPayrollDbContext db,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken) =>
        RunCommandAsync(
            id,
            db,
            () => lifecycle.MarkPastDueAsync(id, GetUserId(principal), cancellationToken),
            cancellationToken);

    private static Task<IResult> EnterGrace(
        Guid id,
        SubscriptionLifecycleService lifecycle,
        MiniPayrollDbContext db,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken) =>
        RunCommandAsync(
            id,
            db,
            () => lifecycle.EnterGraceAsync(id, GetUserId(principal), cancellationToken),
            cancellationToken);

    private static Task<IResult> Suspend(
        Guid id,
        SubscriptionLifecycleService lifecycle,
        MiniPayrollDbContext db,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken) =>
        RunCommandAsync(
            id,
            db,
            () => lifecycle.SuspendAsync(id, GetUserId(principal), cancellationToken),
            cancellationToken);

    private static Task<IResult> Cancel(
        Guid id,
        CancelSubscriptionRequest? request,
        SubscriptionLifecycleService lifecycle,
        MiniPayrollDbContext db,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var actor = GetUserId(principal);
        return RunCommandAsync(
            id,
            db,
            () => request?.AtPeriodEnd == true
                ? lifecycle.RequestCancelAsync(id, actor, cancellationToken)
                : lifecycle.CancelNowAsync(id, actor, cancellationToken),
            cancellationToken);
    }

    private static Task<IResult> Expire(
        Guid id,
        SubscriptionLifecycleService lifecycle,
        MiniPayrollDbContext db,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken) =>
        RunCommandAsync(
            id,
            db,
            () => lifecycle.ExpireAsync(id, GetUserId(principal), cancellationToken),
            cancellationToken);

    private static Task<IResult> Reactivate(
        Guid id,
        SubscriptionLifecycleService lifecycle,
        MiniPayrollDbContext db,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken) =>
        RunCommandAsync(
            id,
            db,
            () => lifecycle.ReactivateAsync(id, GetUserId(principal), cancellationToken),
            cancellationToken);

    private static async Task<IResult> ChangePlan(
        Guid id,
        ChangePlanRequest request,
        SubscriptionLifecycleService lifecycle,
        MiniPayrollDbContext db,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var result = await lifecycle.ChangePlanAsync(
            id,
            request.PlanCode,
            request.BillingCycle,
            GetUserId(principal),
            cancellationToken);
        return result.Status switch
        {
            SubscriptionCommandStatus.Success => Results.Ok(await LoadDetailAsync(id, db, cancellationToken)),
            SubscriptionCommandStatus.CompanyNotFound => Results.NotFound(),
            SubscriptionCommandStatus.Forbidden => Results.Json(
                new { error = "You are not allowed to change this subscription." },
                statusCode: StatusCodes.Status403Forbidden),
            SubscriptionCommandStatus.UsageExceedsPlanLimit => Results.Json(
                new { error = PlanChangeRules.UsageExceededMessage },
                statusCode: StatusCodes.Status409Conflict),
            SubscriptionCommandStatus.ProviderUnavailable => Results.Json(
                new { error = result.Error ?? "Payment provider unavailable." },
                statusCode: StatusCodes.Status503ServiceUnavailable),
            SubscriptionCommandStatus.PaymentFailed => Results.BadRequest(
                new { error = result.Error ?? "The payment could not be completed." }),
            _ => Results.BadRequest(
                new { error = result.Error ?? "This subscription cannot make that change." })
        };
    }

    private static async Task<IResult> RunCommandAsync(
        Guid id,
        MiniPayrollDbContext db,
        Func<Task<SubscriptionCommandResult>> execute,
        CancellationToken cancellationToken)
    {
        var result = await execute();
        return result.Status switch
        {
            SubscriptionCommandStatus.Success => Results.Ok(await LoadDetailAsync(id, db, cancellationToken)),
            SubscriptionCommandStatus.CompanyNotFound => Results.NotFound(),
            SubscriptionCommandStatus.Forbidden => Results.Json(
                new { error = "You are not allowed to change this subscription." },
                statusCode: StatusCodes.Status403Forbidden),
            SubscriptionCommandStatus.AdminRequired => Results.BadRequest(
                new { error = "Create a Company Admin before activating the company." }),
            _ => Results.BadRequest(new { error = "This subscription cannot make that change." })
        };
    }

    private static async Task<CompanyDetail> LoadDetailAsync(
        Guid id,
        MiniPayrollDbContext db,
        CancellationToken cancellationToken)
    {
        var company = await db.Companies
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(item => item.Subscription)
            .ThenInclude(item => item!.Plan)
            .SingleAsync(item => item.Id == id, cancellationToken);
        return await ToDetailAsync(company, db, cancellationToken);
    }

    private static async Task<IResult> UpdateLimit(
        Guid id,
        UpdateLimitRequest request,
        MiniPayrollDbContext db,
        ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        if (!EmployeeLimitRules.IsValid(request.EmployeeLimit))
        {
            return Results.BadRequest(new { error = EmployeeLimitRules.InvalidMessage });
        }

        var company = await db.Companies
            .Include(c => c.Subscription)
            .ThenInclude(s => s!.Plan)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

        if (company is null)
        {
            return Results.NotFound();
        }

        if (company.Subscription is null)
        {
            return Results.BadRequest(new { error = "Company has no subscription." });
        }

        company.Subscription.EmployeeLimit = request.EmployeeLimit;
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            CompanyId = company.Id,
            ActorUserId = GetUserId(principal),
            Action = "company.limit.change",
            Details = request.EmployeeLimit.ToString(),
            OccurredAt = DateTimeOffset.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
        return Results.Ok(await ToDetailAsync(company, db, cancellationToken));
    }

    private static async Task<CompanyDetail> ToDetailAsync(
        Company company,
        MiniPayrollDbContext db,
        CancellationToken cancellationToken)
    {
        var admin = await CompanyAdminLookup.GetAsync(db, company.Id, cancellationToken);
        return new CompanyDetail(
            company.Id,
            company.Name,
            company.ContactEmail,
            company.ContactPhone,
            company.Subscription?.Status.ToString() ?? SubscriptionStatus.Trialing.ToString(),
            company.Subscription?.EmployeeLimit ?? 0,
            company.Subscription?.Plan?.Name ?? PlatformLimits.DefaultPlanName,
            company.IsSetupComplete,
            company.ActivatedAt,
            admin.HasAdmin,
            admin.Email,
            company.Subscription?.CancelAtPeriodEnd ?? false,
            company.Subscription?.TrialEndsAt);
    }

    private static async Task<IResult> ListPayrollRuns(
        Guid id,
        [FromServices] PayrollCalculationService payroll,
        CancellationToken cancellationToken)
    {
        var result = await payroll.ListCompanyRunsAsync(id, cancellationToken);
        return result.Status == PayrollRunStatusCode.Success
            ? Results.Ok(result.Runs)
            : PayrollError(result.Status);
    }

    private static async Task<IResult> ReversePayroll(
        Guid id,
        Guid runId,
        ReversePayrollRequest? request,
        [FromServices] PayrollCalculationService payroll,
        CancellationToken cancellationToken)
    {
        var result = await payroll.ReverseAsync(id, runId, request?.Reason, cancellationToken);
        return result.Status == PayrollRunStatusCode.Success
            ? Results.Ok(result.Run)
            : PayrollError(result.Status);
    }

    private static async Task<IResult> GetBilling(
        Guid id,
        [FromServices] BillingService billing,
        CancellationToken cancellationToken) =>
        BillingHttp(await billing.GetAsync(id, cancellationToken));

    private static async Task<IResult> ListInvoices(
        Guid id,
        InvoiceService invoices,
        CancellationToken cancellationToken) =>
        InvoiceListHttp(await invoices.ListAsync(id, cancellationToken));

    private static async Task<IResult> GetInvoice(
        Guid id,
        Guid invoiceId,
        InvoiceService invoices,
        CancellationToken cancellationToken) =>
        InvoiceHttp(await invoices.GetAsync(id, invoiceId, cancellationToken));

    private static async Task<IResult> CreateInvoice(
        Guid id,
        CreateInvoiceRequest? request,
        InvoiceService invoices,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return InvoiceError(InvoiceCommandStatus.InvalidInput);
        }

        return InvoiceHttp(
            await invoices.CreateAsync(id, request.PeriodStart, request.PeriodEnd, request.Quantity, cancellationToken),
            created: true);
    }

    private static Task<IResult> IssueInvoice(
        Guid id,
        Guid invoiceId,
        InvoiceService invoices,
        CancellationToken cancellationToken) =>
        InvoiceCommandHttp(() => invoices.IssueAsync(id, invoiceId, cancellationToken));

    private static Task<IResult> MarkInvoicePaymentPending(
        Guid id,
        Guid invoiceId,
        InvoiceService invoices,
        CancellationToken cancellationToken) =>
        InvoiceCommandHttp(() => invoices.MarkPaymentPendingAsync(id, invoiceId, cancellationToken));

    private static async Task<IResult> ApplyInvoicePayment(
        Guid id,
        Guid invoiceId,
        ApplyInvoicePaymentRequest? request,
        InvoiceService invoices,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return InvoiceError(InvoiceCommandStatus.InvalidInput);
        }

        return InvoiceHttp(await invoices.ApplyPaymentAsync(id, invoiceId, request.Amount, cancellationToken));
    }

    private static Task<IResult> FailInvoice(
        Guid id,
        Guid invoiceId,
        InvoiceService invoices,
        CancellationToken cancellationToken) =>
        InvoiceCommandHttp(() => invoices.MarkFailedAsync(id, invoiceId, cancellationToken));

    private static Task<IResult> VoidInvoice(
        Guid id,
        Guid invoiceId,
        InvoiceService invoices,
        CancellationToken cancellationToken) =>
        InvoiceCommandHttp(() => invoices.VoidAsync(id, invoiceId, cancellationToken));

    private static Task<IResult> RefundInvoice(
        Guid id,
        Guid invoiceId,
        InvoiceService invoices,
        CancellationToken cancellationToken) =>
        InvoiceCommandHttp(() => invoices.MarkRefundedAsync(id, invoiceId, cancellationToken));

    private static async Task<IResult> InvoiceCommandHttp(Func<Task<InvoiceCommandResult>> execute) =>
        InvoiceHttp(await execute());

    private static IResult InvoiceListHttp(InvoiceListResult result) =>
        result.Status == InvoiceCommandStatus.Success
            ? Results.Ok(result.Invoices)
            : InvoiceError(result.Status);

    private static IResult InvoiceHttp(InvoiceCommandResult result, bool created = false) =>
        result.Status == InvoiceCommandStatus.Success
            ? created
                ? Results.Created($"/api/companies/{result.Invoice!.CompanyId}/invoices/{result.Invoice.Id}", result.Invoice)
                : Results.Ok(result.Invoice)
            : InvoiceError(result.Status);

    private static IResult InvoiceError(InvoiceCommandStatus status) =>
        Results.Json(new { error = status switch
        {
            InvoiceCommandStatus.InvalidInput => "Period or quantity is invalid.",
            InvoiceCommandStatus.InvalidTransition => "This invoice cannot make that change.",
            InvoiceCommandStatus.DuplicatePeriod => "An invoice already exists for this period.",
            InvoiceCommandStatus.Overpay => "Payment exceeds the invoice total.",
            InvoiceCommandStatus.CompanyNotFound or InvoiceCommandStatus.NotFound => "The invoice was not found.",
            InvoiceCommandStatus.Forbidden => "You are not allowed to manage invoices.",
            _ => "The invoice request could not be completed."
        }}, statusCode: InvoiceHttpStatus.For(status));

    private static async Task<IResult> RecordPayment(
        Guid id,
        RecordPaymentRequest? request,
        [FromServices] BillingService billing,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return BillingError(BillingStatusCode.InvalidInput);
        }

        return BillingHttp(await billing.RecordPaymentAsync(id, request, cancellationToken));
    }

    private static async Task<IResult> CreatePaymentLink(
        Guid id,
        CreatePaymentLinkRequest? request,
        [FromServices] PaymentReconciliationService payments,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.BillingPeriod))
        {
            return BillingError(BillingStatusCode.InvalidInput);
        }

        var result = await payments.StartPaymentLinkAsync(id, request.BillingPeriod, cancellationToken);
        return result.Status is PaymentReconciliationStatus.Success or PaymentReconciliationStatus.Duplicate
            ? Results.Ok(new
            {
                paymentLinkUrl = result.CheckoutUrl,
                providerPaymentLinkId = result.ProviderPaymentLinkId,
                invoiceId = result.InvoiceId,
                amount = result.Amount,
                billingPeriod = result.BillingPeriod
            })
            : PaymentLinkError(result);
    }

    private static IResult PaymentLinkError(PaymentReconciliationResult result) =>
        Results.Json(
            new { error = result.Error ?? "The payment link could not be created." },
            statusCode: PaymentHttpStatus.For(result.Status));

    private static IResult BillingHttp(BillingResult result) =>
        result.Status == BillingStatusCode.Success
            ? Results.Ok(result.Billing)
            : BillingError(result.Status);

    private static IResult BillingError(BillingStatusCode status) =>
        Results.Json(new { error = status switch
        {
            BillingStatusCode.InvalidInput => "Amount, payment mode, or invoice reference is invalid.",
            BillingStatusCode.InvalidPeriod => "Choose a billing period from activation through this month.",
            BillingStatusCode.NotActivated => "Activate the company before recording a payment.",
            BillingStatusCode.CompanyNotFound => "The company was not found.",
            BillingStatusCode.Forbidden => "You are not allowed to manage billing.",
            _ => "The billing request could not be completed."
        }}, statusCode: BillingHttpStatus.For(status));

    private static IResult PayrollError(PayrollRunStatusCode status) =>
        Results.Json(new { error = status switch
        {
            PayrollRunStatusCode.InvalidInput => "A reversal reason is required.",
            PayrollRunStatusCode.NotFound => "The payroll run was not found.",
            PayrollRunStatusCode.Forbidden => "You are not allowed to reverse payroll.",
            PayrollRunStatusCode.RunLocked => "Only a finalized payroll run can be reversed.",
            PayrollRunStatusCode.CompanyNotFound => "The company was not found.",
            _ => "The payroll request could not be completed."
        }}, statusCode: PayrollHttpStatus.For(status));

    private static Guid GetUserId(ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    public sealed record CreateCompanyRequest(
        [Required, MinLength(2), MaxLength(200)] string Name,
        [Required, EmailAddress] string ContactEmail,
        string? ContactPhone,
        int EmployeeLimit);

    public sealed record UpdateLimitRequest(int EmployeeLimit);

    public sealed record CreateAdminRequest(
        [Required, EmailAddress] string Email,
        [Required] string TemporaryPassword);

    public sealed record CreateAdminResponse(Guid UserId, string Email);

    public sealed record ReversePayrollRequest(
        [Required, MinLength(1), MaxLength(500)] string Reason);

    public sealed record CancelSubscriptionRequest(bool AtPeriodEnd);

    public sealed record ChangePlanRequest(string PlanCode, BillingCycle? BillingCycle);

    public sealed record CreatePaymentLinkRequest(string BillingPeriod);

    public sealed record CreateInvoiceRequest(
        DateTimeOffset PeriodStart,
        DateTimeOffset PeriodEnd,
        decimal? Quantity);

    public sealed record ApplyInvoicePaymentRequest(decimal Amount);

    public sealed record CompanyListItem(
        Guid Id,
        string Name,
        string ContactEmail,
        string Status,
        int EmployeeLimit,
        bool IsSetupComplete,
        DateTimeOffset? ActivatedAt,
        bool HasAdmin);

    public sealed record CompanyDetail(
        Guid Id,
        string Name,
        string ContactEmail,
        string? ContactPhone,
        string Status,
        int EmployeeLimit,
        string PlanName,
        bool IsSetupComplete,
        DateTimeOffset? ActivatedAt,
        bool HasAdmin,
        string? AdminEmail,
        bool CancelAtPeriodEnd,
        DateTimeOffset? TrialEndsAt);
}

public static class InvoiceHttpStatus
{
    public static int For(InvoiceCommandStatus status) => status switch
    {
        InvoiceCommandStatus.Success => StatusCodes.Status200OK,
        InvoiceCommandStatus.InvalidInput or InvoiceCommandStatus.InvalidTransition or InvoiceCommandStatus.Overpay =>
            StatusCodes.Status400BadRequest,
        InvoiceCommandStatus.DuplicatePeriod => StatusCodes.Status409Conflict,
        InvoiceCommandStatus.CompanyNotFound or InvoiceCommandStatus.NotFound => StatusCodes.Status404NotFound,
        InvoiceCommandStatus.Forbidden => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status500InternalServerError
    };
}

public static class BillingHttpStatus
{
    public static int For(BillingStatusCode status) => status switch
    {
        BillingStatusCode.Success => StatusCodes.Status200OK,
        BillingStatusCode.InvalidInput or BillingStatusCode.InvalidPeriod => StatusCodes.Status400BadRequest,
        BillingStatusCode.CompanyNotFound => StatusCodes.Status404NotFound,
        BillingStatusCode.NotActivated => StatusCodes.Status409Conflict,
        BillingStatusCode.Forbidden => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status500InternalServerError
    };
}
