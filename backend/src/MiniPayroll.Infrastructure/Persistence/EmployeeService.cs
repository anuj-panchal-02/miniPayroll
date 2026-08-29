using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Auth;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Tenancy;

namespace MiniPayroll.Infrastructure.Persistence;

public enum EmployeeStatusCode
{
    Success,
    CompanyNotFound,
    SetupIncomplete,
    SubscriptionReadOnly,
    InvalidInput,
    DuplicateEmployeeCode,
    EmployeeLimitReached,
    NotFound
}

public sealed record EmployeeInput(
    string? EmployeeCode,
    string? FullName,
    DateOnly? DateOfBirth,
    string? Phone,
    string? Email,
    string? AddressLine1,
    string? AddressLine2,
    string? City,
    string? State,
    string? PostalCode,
    string? Designation,
    string? Department,
    DateOnly? JoiningDate,
    DateOnly? ExitDate,
    EmployeeStatus? Status,
    string? BankName,
    string? BankAccountNumber,
    string? Ifsc,
    string? UpiId,
    decimal? OvertimeRate,
    bool SaveAsDraft = false,
    int? DraftStep = null,
    SalaryStructureInput? SalaryStructure = null);

public sealed record EmployeeListItem(
    Guid Id,
    string EmployeeCode,
    string FullName,
    string Designation,
    string? Department,
    EmployeeStatus Status,
    DateOnly? JoiningDate,
    string MaskedAccountNumber);

public sealed record EmployeeDetail(
    Guid Id,
    string EmployeeCode,
    string FullName,
    DateOnly? DateOfBirth,
    string Phone,
    string Email,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string State,
    string PostalCode,
    string Designation,
    string? Department,
    EmploymentType EmploymentType,
    DateOnly? JoiningDate,
    DateOnly? ExitDate,
    EmployeeStatus Status,
    int? DraftStep,
    string BankName,
    string BankAccountNumber,
    string MaskedAccountNumber,
    string Ifsc,
    string? UpiId,
    decimal? OvertimeRate);

public sealed record EmployeeListState(
    IReadOnlyList<EmployeeListItem> Employees,
    int ActiveCount,
    int EmployeeLimit);

public sealed record EmployeeResult(
    EmployeeStatusCode Status,
    EmployeeDetail? Employee = null,
    EmployeeListState? List = null,
    int? EmployeeLimit = null);

public sealed class EmployeeService(
    MiniPayrollDbContext db,
    ITenantContext tenant,
    SalaryStructureService? salaryStructures = null)
{
    public const string LimitReachedMessage =
        "Employee limit reached. Your current plan supports {0} employees. Please contact your service provider to increase the limit.";

    public async Task<EmployeeResult> ListAsync(CancellationToken cancellationToken = default)
    {
        var context = await GetWriteContextAsync(requireMutation: false, cancellationToken);
        if (context.Status != EmployeeStatusCode.Success || context.Company is null)
        {
            return new EmployeeResult(context.Status);
        }

        var employees = await db.Employees
            .AsNoTracking()
            .Where(employee => employee.CompanyId == context.Company.Id)
            .OrderBy(employee => employee.EmployeeCode)
            .ToListAsync(cancellationToken);

        var items = employees.Select(ToListItem).ToList();
        var activeCount = employees.Count(employee => employee.Status == EmployeeStatus.Active);
        var limit = context.Company.Subscription?.EmployeeLimit ?? 0;
        return new EmployeeResult(
            EmployeeStatusCode.Success,
            List: new EmployeeListState(items, activeCount, limit));
    }

    public async Task<EmployeeResult> GetAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var context = await GetWriteContextAsync(requireMutation: false, cancellationToken);
        if (context.Status != EmployeeStatusCode.Success || context.Company is null)
        {
            return new EmployeeResult(context.Status);
        }

        var employee = await db.Employees
            .SingleOrDefaultAsync(
                item => item.Id == id && item.CompanyId == context.Company.Id,
                cancellationToken);
        return employee is null
            ? new EmployeeResult(EmployeeStatusCode.NotFound)
            : new EmployeeResult(EmployeeStatusCode.Success, ToDetail(employee));
    }

    public async Task<EmployeeResult> CreateAsync(
        EmployeeInput? input,
        CancellationToken cancellationToken = default)
    {
        var context = await GetWriteContextAsync(requireMutation: true, cancellationToken);
        if (context.Status != EmployeeStatusCode.Success || context.Company is null)
        {
            return new EmployeeResult(context.Status, EmployeeLimit: context.EmployeeLimit);
        }

        var employee = Apply(new Employee
        {
            Id = Guid.NewGuid(),
            CompanyId = context.Company.Id,
            EmploymentType = EmploymentType.FullTimeMonthly,
            CreatedAt = DateTimeOffset.UtcNow
        }, input);
        employee.EmploymentType = EmploymentType.FullTimeMonthly;

        if (input?.SaveAsDraft == true)
        {
            employee.Status = EmployeeStatus.Draft;
            employee.DraftStep = EmployeeRules.ClampDraftStep(input.DraftStep);
            if (!EmployeeRules.IsValidDraft(employee))
            {
                return new EmployeeResult(EmployeeStatusCode.InvalidInput);
            }

            if (await CodeTakenAsync(context.Company.Id, employee.EmployeeCode, null, cancellationToken))
            {
                return new EmployeeResult(EmployeeStatusCode.DuplicateEmployeeCode);
            }

            db.Employees.Add(employee);
            AddAudit(AuditActions.EmployeeCreate, employee.EmployeeCode);
            await db.SaveChangesAsync(cancellationToken);
            return new EmployeeResult(EmployeeStatusCode.Success, ToDetail(employee));
        }

        employee.Status = EmployeeStatus.Active;
        employee.DraftStep = null;
        if (!EmployeeRules.IsValid(employee))
        {
            return new EmployeeResult(EmployeeStatusCode.InvalidInput);
        }

        var limit = context.Company.Subscription!.EmployeeLimit;
        var activeCount = await db.Employees.CountAsync(
            item => item.CompanyId == context.Company.Id && item.Status == EmployeeStatus.Active,
            cancellationToken);
        if (activeCount >= limit)
        {
            return new EmployeeResult(EmployeeStatusCode.EmployeeLimitReached, EmployeeLimit: limit);
        }

        if (await CodeTakenAsync(context.Company.Id, employee.EmployeeCode, null, cancellationToken))
        {
            return new EmployeeResult(EmployeeStatusCode.DuplicateEmployeeCode);
        }

        if (!await SalaryStructures.AddInitialStructureAsync(
            employee, input?.SalaryStructure, cancellationToken))
        {
            return new EmployeeResult(EmployeeStatusCode.InvalidInput);
        }

        db.Employees.Add(employee);
        AddAudit(AuditActions.EmployeeCreate, employee.EmployeeCode);
        await db.SaveChangesAsync(cancellationToken);
        return new EmployeeResult(EmployeeStatusCode.Success, ToDetail(employee));
    }

    public async Task<EmployeeResult> UpdateAsync(
        Guid id,
        EmployeeInput? input,
        CancellationToken cancellationToken = default)
    {
        var context = await GetWriteContextAsync(requireMutation: true, cancellationToken);
        if (context.Status != EmployeeStatusCode.Success || context.Company is null)
        {
            return new EmployeeResult(context.Status, EmployeeLimit: context.EmployeeLimit);
        }

        var employee = await db.Employees.SingleOrDefaultAsync(
            item => item.Id == id && item.CompanyId == context.Company.Id,
            cancellationToken);
        if (employee is null)
        {
            return new EmployeeResult(EmployeeStatusCode.NotFound);
        }

        var previousStatus = employee.Status;
        var previousDraftStep = employee.DraftStep;

        if (input?.SaveAsDraft == true)
        {
            if (previousStatus != EmployeeStatus.Draft)
            {
                return new EmployeeResult(EmployeeStatusCode.InvalidInput);
            }

            Apply(employee, input);
            employee.EmploymentType = EmploymentType.FullTimeMonthly;
            employee.Status = EmployeeStatus.Draft;
            employee.DraftStep = EmployeeRules.ClampDraftStep(input.DraftStep);
            if (!EmployeeRules.IsValidDraft(employee))
            {
                return new EmployeeResult(EmployeeStatusCode.InvalidInput);
            }

            if (await CodeTakenAsync(context.Company.Id, employee.EmployeeCode, employee.Id, cancellationToken))
            {
                return new EmployeeResult(EmployeeStatusCode.DuplicateEmployeeCode);
            }

            AddAudit(AuditActions.EmployeeUpdate, employee.EmployeeCode);
            await db.SaveChangesAsync(cancellationToken);
            return new EmployeeResult(EmployeeStatusCode.Success, ToDetail(employee));
        }

        var nextStatus = previousStatus == EmployeeStatus.Draft
            ? EmployeeStatus.Active
            : input?.Status ?? previousStatus;
        if (nextStatus == EmployeeStatus.Draft)
        {
            nextStatus = previousStatus;
        }

        var limit = context.Company.Subscription!.EmployeeLimit;
        var becomingActive = previousStatus != EmployeeStatus.Active && nextStatus == EmployeeStatus.Active;
        if (becomingActive)
        {
            var activeCount = await db.Employees.CountAsync(
                item => item.CompanyId == context.Company.Id
                    && item.Status == EmployeeStatus.Active
                    && item.Id != employee.Id,
                cancellationToken);
            if (activeCount >= limit)
            {
                return new EmployeeResult(EmployeeStatusCode.EmployeeLimitReached, EmployeeLimit: limit);
            }
        }

        Apply(employee, input);
        employee.EmploymentType = EmploymentType.FullTimeMonthly;
        employee.Status = nextStatus;
        employee.DraftStep = null;

        if (!EmployeeRules.IsValid(employee))
        {
            employee.Status = previousStatus;
            employee.DraftStep = previousDraftStep;
            return new EmployeeResult(EmployeeStatusCode.InvalidInput);
        }

        if (await CodeTakenAsync(context.Company.Id, employee.EmployeeCode, employee.Id, cancellationToken))
        {
            employee.Status = previousStatus;
            employee.DraftStep = previousDraftStep;
            return new EmployeeResult(EmployeeStatusCode.DuplicateEmployeeCode);
        }

        if (becomingActive)
        {
            var hasSalary = input?.SalaryStructure is { }
                ? await SalaryStructures.AddInitialStructureAsync(employee, input.SalaryStructure, cancellationToken)
                : await SalaryStructures.HasStructureAsync(employee.Id, cancellationToken);
            if (!hasSalary)
            {
                employee.Status = previousStatus;
                employee.DraftStep = previousDraftStep;
                return new EmployeeResult(EmployeeStatusCode.InvalidInput);
            }
        }

        var action = previousStatus == EmployeeStatus.Active && employee.Status == EmployeeStatus.Inactive
            ? AuditActions.EmployeeDeactivate
            : AuditActions.EmployeeUpdate;
        AddAudit(action, employee.EmployeeCode);
        await db.SaveChangesAsync(cancellationToken);
        return new EmployeeResult(EmployeeStatusCode.Success, ToDetail(employee));
    }

    private async Task<(EmployeeStatusCode Status, Company? Company, int? EmployeeLimit)> GetWriteContextAsync(
        bool requireMutation,
        CancellationToken cancellationToken)
    {
        if (tenant.IsSuperadmin || tenant.CompanyId is not { } companyId)
        {
            return (EmployeeStatusCode.CompanyNotFound, null, null);
        }

        var company = await db.Companies
            .Include(item => item.Subscription)
            .SingleOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        if (company is null)
        {
            return (EmployeeStatusCode.CompanyNotFound, null, null);
        }

        if (!company.IsSetupComplete)
        {
            return (EmployeeStatusCode.SetupIncomplete, company, company.Subscription?.EmployeeLimit);
        }

        if (requireMutation && !SubscriptionMutationRules.CanMutate(company.Subscription?.Status))
        {
            return (EmployeeStatusCode.SubscriptionReadOnly, company, company.Subscription?.EmployeeLimit);
        }

        return (EmployeeStatusCode.Success, company, company.Subscription?.EmployeeLimit);
    }

    private SalaryStructureService SalaryStructures => salaryStructures ?? new SalaryStructureService(db, tenant);

    private Task<bool> CodeTakenAsync(
        Guid companyId,
        string employeeCode,
        Guid? excludingId,
        CancellationToken cancellationToken) =>
        db.Employees.AnyAsync(
            item => item.CompanyId == companyId
                && item.EmployeeCode == employeeCode
                && (!excludingId.HasValue || item.Id != excludingId.Value),
            cancellationToken);

    private static Employee Apply(Employee employee, EmployeeInput? input)
    {
        employee.EmployeeCode = input?.EmployeeCode ?? string.Empty;
        employee.FullName = input?.FullName ?? string.Empty;
        employee.DateOfBirth = input?.DateOfBirth;
        employee.Phone = input?.Phone ?? string.Empty;
        employee.Email = input?.Email ?? string.Empty;
        employee.AddressLine1 = input?.AddressLine1 ?? string.Empty;
        employee.AddressLine2 = input?.AddressLine2;
        employee.City = input?.City ?? string.Empty;
        employee.State = input?.State ?? string.Empty;
        employee.PostalCode = input?.PostalCode ?? string.Empty;
        employee.Designation = input?.Designation ?? string.Empty;
        employee.Department = input?.Department;
        employee.JoiningDate = input?.JoiningDate;
        employee.ExitDate = input?.ExitDate;
        employee.Status = input?.Status ?? employee.Status;
        employee.BankName = input?.BankName ?? string.Empty;
        employee.BankAccountNumber = input?.BankAccountNumber ?? string.Empty;
        employee.Ifsc = input?.Ifsc ?? string.Empty;
        employee.UpiId = input?.UpiId;
        employee.OvertimeRate = input?.OvertimeRate;
        EmployeeRules.Normalize(employee);
        return employee;
    }

    private void AddAudit(string action, string details)
    {
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            CompanyId = tenant.CompanyId,
            ActorUserId = tenant.UserId,
            Action = action,
            Details = details,
            OccurredAt = DateTimeOffset.UtcNow
        });
    }

    private static EmployeeListItem ToListItem(Employee employee) => new(
        employee.Id,
        employee.EmployeeCode,
        employee.FullName,
        employee.Designation,
        employee.Department,
        employee.Status,
        employee.JoiningDate,
        EmployeeRules.MaskAccountNumber(employee.BankAccountNumber));

    private static EmployeeDetail ToDetail(Employee employee) => new(
        employee.Id,
        employee.EmployeeCode,
        employee.FullName,
        employee.DateOfBirth,
        employee.Phone,
        employee.Email,
        employee.AddressLine1,
        employee.AddressLine2,
        employee.City,
        employee.State,
        employee.PostalCode,
        employee.Designation,
        employee.Department,
        employee.EmploymentType,
        employee.JoiningDate,
        employee.ExitDate,
        employee.Status,
        employee.DraftStep,
        employee.BankName,
        employee.BankAccountNumber,
        EmployeeRules.MaskAccountNumber(employee.BankAccountNumber),
        employee.Ifsc,
        employee.UpiId,
        employee.OvertimeRate);
}
