using Microsoft.EntityFrameworkCore;
using MiniPayroll.Domain.Entities;
using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Payroll;
using MiniPayroll.Domain.Payroll.Statutory;

namespace MiniPayroll.Infrastructure.Persistence;

internal sealed record PayrollLiveSources(
    PayrollPeriod Period,
    IReadOnlyList<Employee> Employees,
    IReadOnlyDictionary<Guid, SalaryStructure> Structures,
    IReadOnlyDictionary<Guid, MonthlyAttendance> Attendance,
    IReadOnlyList<Overtime> Overtime,
    IReadOnlyList<Bonus> Bonuses,
    IReadOnlyList<Deduction> Deductions,
    IReadOnlyList<PayrollStatutoryOverride> Overrides);

internal static class PayrollSourceLoader
{
    public static async Task<PayrollLiveSources> LoadAsync(
        MiniPayrollDbContext db,
        Guid companyId,
        PayrollRun run,
        CancellationToken cancellationToken)
    {
        var period = new PayrollPeriod(run.Year, run.Month);
        var employees = (await db.Employees
                .Where(employee => employee.CompanyId == companyId
                    && employee.Status != EmployeeStatus.Draft)
                .OrderBy(employee => employee.EmployeeCode)
                .ToListAsync(cancellationToken))
            .Where(employee => PayrollEligibility.IsEligible(
                employee.Status, employee.JoiningDate, employee.ExitDate, period))
            .ToList();

        var employeeIds = employees.Select(employee => employee.Id).ToList();
        var structures = (await db.SalaryStructures
                .AsNoTracking()
                .Include(structure => structure.Components)
                .Where(structure => employeeIds.Contains(structure.EmployeeId)
                    && structure.EffectiveFrom <= period.LastDay)
                .ToListAsync(cancellationToken))
            .GroupBy(structure => structure.EmployeeId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(structure => structure.EffectiveFrom).First());

        var attendance = await db.MonthlyAttendance
            .Where(item => item.PayrollRunId == run.Id)
            .ToDictionaryAsync(item => item.EmployeeId, cancellationToken);
        var overtime = await db.Overtime
            .Where(item => item.PayrollRunId == run.Id)
            .ToListAsync(cancellationToken);
        var bonuses = await db.Bonuses
            .Where(item => item.PayrollRunId == run.Id)
            .ToListAsync(cancellationToken);
        var deductions = await db.Deductions
            .Where(item => item.PayrollRunId == run.Id)
            .ToListAsync(cancellationToken);
        var overrides = await db.PayrollStatutoryOverrides
            .Where(item => item.PayrollRunId == run.Id)
            .ToListAsync(cancellationToken);

        return new PayrollLiveSources(
            period,
            employees,
            structures,
            attendance,
            overtime,
            bonuses,
            deductions,
            overrides);
    }

    public static PayrollSourceSnapshot Snapshot(
        PayrollRun run,
        Company company,
        PayrollLiveSources sources,
        IStatutoryRuleProvider? rules = null)
    {
        var provider = rules ?? ConfiguredStatutoryRuleProvider.Instance;
        var asOf = sources.Period.LastDay;
        var pf = provider.PfFor(asOf);
        var esi = provider.EsiFor(asOf);
        return new PayrollSourceSnapshot(
            run.DailyRateMethod,
            asOf,
            pf.EffectiveFrom,
            esi.EffectiveFrom,
            pf.EmployeeRate,
            pf.EmployerRate,
            pf.WageCeiling,
            esi.EmployeeRate,
            esi.EmployerRate,
            esi.EligibilityCeiling,
            new PayrollCompanyStatutorySource(
                company.PfApplicable,
                company.PfUseWageCeiling,
                company.EsiApplicable,
                company.State),
            sources.Employees
                .OrderBy(employee => employee.Id)
                .Select(employee => MapEmployee(employee, sources))
                .ToList());
    }

    public static async Task<bool> HasDriftAsync(
        MiniPayrollDbContext db,
        Company company,
        PayrollRun run,
        CancellationToken cancellationToken)
    {
        if (run.Status != PayrollRunStatus.Calculated)
        {
            return false;
        }

        var sources = await LoadAsync(db, company.Id, run, cancellationToken);
        return PayrollSourceFingerprint.HasDrift(
            run.SourceFingerprint,
            Snapshot(run, company, sources));
    }

    private static PayrollEmployeeSource MapEmployee(Employee employee, PayrollLiveSources sources)
    {
        sources.Structures.TryGetValue(employee.Id, out var structure);
        sources.Attendance.TryGetValue(employee.Id, out var attendance);
        var overtime = sources.Overtime
            .Where(item => item.EmployeeId == employee.Id)
            .OrderBy(item => item.Hours)
            .ThenBy(item => item.Rate)
            .Select(item => new PayrollOvertimeSource(item.Hours, item.Rate, item.Rate ?? employee.OvertimeRate))
            .ToList();
        var bonuses = sources.Bonuses
            .Where(item => item.EmployeeId == employee.Id)
            .OrderBy(item => item.Type)
            .ThenBy(item => item.Amount)
            .Select(item => new PayrollBonusSource(item.Type, item.Amount))
            .ToList();
        var deductions = sources.Deductions
            .Where(item => item.EmployeeId == employee.Id)
            .OrderBy(item => item.Type)
            .ThenBy(item => item.Amount)
            .Select(item => new PayrollDeductionSource(item.Type, item.Amount))
            .ToList();
        var overrides = sources.Overrides
            .Where(item => item.EmployeeId == employee.Id)
            .OrderBy(item => item.Kind)
            .Select(item => new PayrollOverrideSource(item.Kind, item.Amount))
            .ToList();

        return new PayrollEmployeeSource(
            employee.Id,
            employee.EmployeeCode,
            employee.FullName,
            employee.Designation,
            employee.JoiningDate,
            employee.ExitDate,
            employee.Status,
            employee.Gender,
            employee.PfCovered,
            employee.EsiCovered,
            employee.OvertimeRate,
            structure?.EffectiveFrom,
            structure?.Components
                .OrderBy(component => component.SortOrder)
                .Select(component => new PayrollStructureComponentSource(
                    component.Name,
                    component.Type,
                    component.ValueType,
                    component.Value,
                    component.SortOrder,
                    component.Kind))
                .ToList() ?? [],
            attendance is null
                ? null
                : new PayrollAttendanceSource(
                    attendance.WorkingDays,
                    attendance.Present,
                    attendance.PaidLeave,
                    attendance.UnpaidLeave),
            overtime,
            bonuses,
            deductions,
            overrides);
    }
}
