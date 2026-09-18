using MiniPayroll.Domain.Enums;
using MiniPayroll.Domain.Payroll.Statutory;

namespace MiniPayroll.Domain.Payroll;

internal sealed record PayrollValidation(
    IReadOnlyList<string> Errors,
    int DaysEmployed,
    IReadOnlyList<PayrollStructureLine> Structure,
    PayrollStructureLine? Basic,
    IReadOnlyList<PayrollOvertimeEntry> Overtime);

internal static class PayrollInputValidator
{
    public static PayrollValidation Validate(
        PayrollEmployeeInput input,
        IStatutoryRuleProvider? rules = null)
    {
        var provider = rules ?? ConfiguredStatutoryRuleProvider.Instance;
        var errors = new List<string>();
        var period = input.Period;
        var attendance = input.Attendance;

        if (attendance.Present + attendance.PaidLeave + attendance.UnpaidLeave != attendance.WorkingDays)
        {
            errors.Add(PayrollCalculationMessages.AttendanceIdentity);
        }

        if (attendance.Present < 0 || attendance.PaidLeave < 0 || attendance.UnpaidLeave < 0)
        {
            errors.Add(PayrollCalculationMessages.NegativeAttendance);
        }

        if (attendance.WorkingDays > period.CalendarDays)
        {
            errors.Add(PayrollCalculationMessages.WorkingDaysExceedCalendar);
        }

        if (attendance.UnpaidLeave > attendance.WorkingDays)
        {
            errors.Add(PayrollCalculationMessages.UnpaidLeaveExceedsWorkingDays);
        }

        var computedDaysEmployed = period.DaysEmployed(input.JoiningDate, input.ExitDate);
        var daysEmployed = computedDaysEmployed ?? 0;
        if (computedDaysEmployed is null)
        {
            errors.Add(PayrollCalculationMessages.MissingJoiningDate);
        }
        else if (computedDaysEmployed == 0)
        {
            errors.Add(PayrollCalculationMessages.NoEmploymentOverlap);
        }

        if (computedDaysEmployed is int employed && attendance.UnpaidLeave > employed)
        {
            errors.Add(PayrollCalculationMessages.UnpaidLeaveExceedsDaysEmployed);
        }

        if (input.Statutory is { } statutoryPolicy
            && provider.ProfessionalTaxRequiresGender(statutoryPolicy.CompanyState)
            && statutoryPolicy.Gender is not Gender.Male and not Gender.Female)
        {
            errors.Add(PayrollCalculationMessages.MissingGenderForProfessionalTax);
        }

        PayrollStructureLine? basic = null;
        IReadOnlyList<PayrollStructureLine> structure;
        if (input.StructureLines is not { Count: > 0 } lines)
        {
            errors.Add(PayrollCalculationMessages.MissingStructure);
            structure = [];
        }
        else
        {
            structure = lines;
            basic = structure.FirstOrDefault(line =>
                StructureLineKinds.Resolve(line) == SalaryComponentKind.Basic
                && line.Type == SalaryComponentType.Earning
                && line.ValueType == SalaryComponentValueType.FixedAmount);
            if (basic is null)
            {
                errors.Add(PayrollCalculationMessages.MissingBasicSalary);
            }
        }

        var overtime = input.Overtime ?? [];
        if (overtime.Any(entry => entry.Hours > 0 && entry.Rate is not > 0m))
        {
            errors.Add(PayrollCalculationMessages.MissingOvertimeRate);
        }

        return new PayrollValidation(errors, daysEmployed, structure, basic, overtime);
    }
}
