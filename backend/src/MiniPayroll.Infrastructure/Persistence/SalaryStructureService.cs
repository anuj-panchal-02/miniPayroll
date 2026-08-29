using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Auth;
using MiniPayroll.Domain.Constants;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Tenancy;

namespace MiniPayroll.Infrastructure.Persistence;

public sealed record SalaryStructureComponentInput(
    string? Name,
    SalaryComponentType Type,
    SalaryComponentValueType ValueType,
    decimal Value,
    int SortOrder);

public sealed record SalaryStructureInput(
    DateOnly? EffectiveFrom,
    IReadOnlyList<SalaryStructureComponentInput>? Components);

public sealed record SalaryStructureComponentDetail(
    Guid Id,
    string Name,
    SalaryComponentType Type,
    SalaryComponentValueType ValueType,
    decimal Value,
    int SortOrder);

public sealed record SalaryStructureDetail(
    Guid Id,
    Guid EmployeeId,
    DateOnly EffectiveFrom,
    DateTimeOffset CreatedAt,
    IReadOnlyList<SalaryStructureComponentDetail> Components,
    decimal RecurringEarnings,
    decimal RecurringDeductions);

public enum SalaryStructureStatusCode
{
    Success,
    CompanyNotFound,
    SetupIncomplete,
    SubscriptionReadOnly,
    EmployeeNotFound,
    InvalidInput,
    DuplicateEffectiveDate,
    NotFound
}

public sealed record SalaryStructureResult(
    SalaryStructureStatusCode Status,
    SalaryStructureDetail? Structure = null,
    IReadOnlyList<SalaryStructureDetail>? Structures = null);

public sealed class SalaryStructureService(
    MiniPayrollDbContext db,
    ITenantContext tenant)
{
    public static readonly IReadOnlySet<string> StandardPresetNames = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        "Basic Salary", "HRA", "Conveyance Allowance", "Special Allowance",
        "Provident Fund (PF)", "ESI", "Professional Tax", "Labour Welfare Fund (LWF)"
    };

    public async Task<SalaryStructureResult> ListAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        var context = await GetContextAsync(false, cancellationToken);
        if (context.Status != SalaryStructureStatusCode.Success || context.Company is null)
        {
            return new SalaryStructureResult(context.Status);
        }

        var employeeExists = await db.Employees.AnyAsync(
            employee => employee.Id == employeeId && employee.CompanyId == context.Company.Id,
            cancellationToken);
        if (!employeeExists)
        {
            return new SalaryStructureResult(SalaryStructureStatusCode.EmployeeNotFound);
        }

        var structures = await StructuresForEmployee(employeeId)
            .OrderByDescending(structure => structure.EffectiveFrom)
            .ToListAsync(cancellationToken);
        return new SalaryStructureResult(
            SalaryStructureStatusCode.Success,
            Structures: structures.Select(ToDetail).ToList());
    }

    public async Task<SalaryStructureResult> GetEffectiveAsync(
        Guid employeeId,
        DateOnly? effectiveOn,
        CancellationToken cancellationToken = default)
    {
        var context = await GetContextAsync(false, cancellationToken);
        if (context.Status != SalaryStructureStatusCode.Success || context.Company is null)
        {
            return new SalaryStructureResult(context.Status);
        }

        var employeeExists = await db.Employees.AnyAsync(
            employee => employee.Id == employeeId && employee.CompanyId == context.Company.Id,
            cancellationToken);
        if (!employeeExists)
        {
            return new SalaryStructureResult(SalaryStructureStatusCode.EmployeeNotFound);
        }

        var date = effectiveOn ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var structure = await StructuresForEmployee(employeeId)
            .Where(item => item.EffectiveFrom <= date)
            .OrderByDescending(item => item.EffectiveFrom)
            .FirstOrDefaultAsync(cancellationToken);
        return structure is null
            ? new SalaryStructureResult(SalaryStructureStatusCode.NotFound)
            : new SalaryStructureResult(SalaryStructureStatusCode.Success, ToDetail(structure));
    }

    public async Task<SalaryStructureResult> CreateAsync(
        Guid employeeId,
        SalaryStructureInput? input,
        CancellationToken cancellationToken = default)
    {
        var context = await GetContextAsync(true, cancellationToken);
        if (context.Status != SalaryStructureStatusCode.Success || context.Company is null)
        {
            return new SalaryStructureResult(context.Status);
        }

        var employee = await db.Employees.SingleOrDefaultAsync(
            item => item.Id == employeeId && item.CompanyId == context.Company.Id,
            cancellationToken);
        if (employee is null)
        {
            return new SalaryStructureResult(SalaryStructureStatusCode.EmployeeNotFound);
        }

        if (!IsValid(input, employee.JoiningDate))
        {
            return new SalaryStructureResult(SalaryStructureStatusCode.InvalidInput);
        }

        if (await db.SalaryStructures.AnyAsync(
            structure => structure.EmployeeId == employeeId
                && structure.EffectiveFrom == input!.EffectiveFrom!.Value,
            cancellationToken))
        {
            return new SalaryStructureResult(SalaryStructureStatusCode.DuplicateEffectiveDate);
        }

        var structure = await AddStructureAsync(employee, input!, cancellationToken);
        AddAudit(employee.EmployeeCode, structure.EffectiveFrom);
        await db.SaveChangesAsync(cancellationToken);
        return new SalaryStructureResult(SalaryStructureStatusCode.Success, ToDetail(structure));
    }

    public async Task<bool> AddInitialStructureAsync(
        Employee employee,
        SalaryStructureInput? input,
        CancellationToken cancellationToken = default)
    {
        if (!IsValid(input, employee.JoiningDate))
        {
            return false;
        }
        var initialInput = input!;

        if (await db.SalaryStructures.AnyAsync(
            structure => structure.EmployeeId == employee.Id
                && structure.EffectiveFrom == initialInput.EffectiveFrom!.Value,
            cancellationToken))
        {
            return false;
        }

        await AddStructureAsync(employee, initialInput, cancellationToken);
        AddAudit(employee.EmployeeCode, initialInput.EffectiveFrom!.Value);
        return true;
    }

    public Task<bool> HasStructureAsync(Guid employeeId, CancellationToken cancellationToken = default) =>
        db.SalaryStructures.AnyAsync(structure => structure.EmployeeId == employeeId, cancellationToken);

    public static bool IsValid(SalaryStructureInput? input, DateOnly? joiningDate)
    {
        if (input?.EffectiveFrom is not { } effectiveFrom
            || input.Components is not { Count: > 0 }
            || (joiningDate.HasValue && effectiveFrom < joiningDate.Value))
        {
            return false;
        }

        var basicCount = 0;
        var sortOrders = new HashSet<int>();
        foreach (var component in input.Components)
        {
            var name = component.Name?.Trim();
            if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || component.Value <= 0
                || (component.ValueType == SalaryComponentValueType.PercentageOfBasic
                    && component.Value > 100)
                || component.SortOrder < 0 || !sortOrders.Add(component.SortOrder))
            {
                return false;
            }

            if (name.Equals("Basic Salary", StringComparison.OrdinalIgnoreCase))
            {
                if (component.Type != SalaryComponentType.Earning
                    || component.ValueType != SalaryComponentValueType.FixedAmount)
                {
                    return false;
                }
                basicCount++;
            }
        }

        return basicCount == 1;
    }

    private async Task<SalaryStructure> AddStructureAsync(
        Employee employee,
        SalaryStructureInput input,
        CancellationToken cancellationToken)
    {
        var structure = new SalaryStructure
        {
            Id = Guid.NewGuid(),
            CompanyId = employee.CompanyId,
            EmployeeId = employee.Id,
            EffectiveFrom = input.EffectiveFrom!.Value,
            CreatedAt = DateTimeOffset.UtcNow
        };

        foreach (var inputComponent in input.Components!.OrderBy(component => component.SortOrder))
        {
            var name = inputComponent.Name!.Trim();
            var component = await db.SalaryComponents.SingleOrDefaultAsync(
                item => item.CompanyId == employee.CompanyId
                    && item.Name == name
                    && item.Type == inputComponent.Type,
                cancellationToken);
            if (component is null)
            {
                component = new SalaryComponent
                {
                    Id = Guid.NewGuid(),
                    CompanyId = employee.CompanyId,
                    Name = name,
                    Type = inputComponent.Type,
                    IsStandardPreset = StandardPresetNames.Contains(name),
                    CreatedAt = DateTimeOffset.UtcNow
                };
                db.SalaryComponents.Add(component);
            }

            structure.Components.Add(new EmployeeSalaryComponent
            {
                Id = Guid.NewGuid(),
                SalaryComponentId = component.Id,
                Name = component.Name,
                Type = inputComponent.Type,
                ValueType = inputComponent.ValueType,
                Value = decimal.Round(inputComponent.Value, 2, MidpointRounding.AwayFromZero),
                SortOrder = inputComponent.SortOrder
            });
        }

        db.SalaryStructures.Add(structure);
        return structure;
    }

    private IQueryable<SalaryStructure> StructuresForEmployee(Guid employeeId) =>
        db.SalaryStructures
            .AsNoTracking()
            .Include(structure => structure.Components)
            .Where(structure => structure.EmployeeId == employeeId);

    private async Task<(SalaryStructureStatusCode Status, Company? Company)> GetContextAsync(
        bool requireMutation,
        CancellationToken cancellationToken)
    {
        if (tenant.IsSuperadmin || tenant.CompanyId is not { } companyId)
        {
            return (SalaryStructureStatusCode.CompanyNotFound, null);
        }

        var company = await db.Companies.Include(item => item.Subscription)
            .SingleOrDefaultAsync(item => item.Id == companyId, cancellationToken);
        if (company is null)
        {
            return (SalaryStructureStatusCode.CompanyNotFound, null);
        }
        if (!company.IsSetupComplete)
        {
            return (SalaryStructureStatusCode.SetupIncomplete, company);
        }
        if (requireMutation && !SubscriptionMutationRules.CanMutate(company.Subscription?.Status))
        {
            return (SalaryStructureStatusCode.SubscriptionReadOnly, company);
        }
        return (SalaryStructureStatusCode.Success, company);
    }

    private void AddAudit(string employeeCode, DateOnly effectiveFrom)
    {
        db.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            CompanyId = tenant.CompanyId,
            ActorUserId = tenant.UserId,
            Action = AuditActions.SalaryStructureCreate,
            Details = $"{employeeCode} effective {effectiveFrom:yyyy-MM-dd}",
            OccurredAt = DateTimeOffset.UtcNow
        });
    }

    private static SalaryStructureDetail ToDetail(SalaryStructure structure)
    {
        var components = structure.Components.OrderBy(component => component.SortOrder)
            .Select(component => new SalaryStructureComponentDetail(
                component.Id, component.Name, component.Type, component.ValueType,
                component.Value, component.SortOrder))
            .ToList();
        var basicSalary = components.Single(component =>
            component.Name.Equals("Basic Salary", StringComparison.OrdinalIgnoreCase)).Value;
        decimal EffectiveValue(SalaryStructureComponentDetail component) =>
            component.ValueType == SalaryComponentValueType.FixedAmount
                ? component.Value
                : decimal.Round(basicSalary * component.Value / 100m, 2, MidpointRounding.AwayFromZero);
        return new SalaryStructureDetail(
            structure.Id,
            structure.EmployeeId,
            structure.EffectiveFrom,
            structure.CreatedAt,
            components,
            components.Where(component => component.Type == SalaryComponentType.Earning)
                .Sum(EffectiveValue),
            components.Where(component => component.Type == SalaryComponentType.Deduction)
                .Sum(EffectiveValue));
    }
}
