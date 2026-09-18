namespace MiniPayroll.Domain.Payroll;

public static class PayrollCalculationMessages
{
    public const string AttendanceIdentity =
        "Attendance identity violated: Present + Paid Leave + Unpaid Leave must equal Working Days.";
    public const string MissingJoiningDate = "Employee has no joining date.";
    public const string NoEmploymentOverlap = "Employee was not employed during this payroll period.";
    public const string MissingStructure = "Employee has no salary structure configured.";
    public const string MissingBasicSalary = "Salary structure has no fixed Basic Salary earning.";
    public const string MissingOvertimeRate = "Overtime hours entered without an overtime rate.";
    public const string NegativeNet = "Net salary is negative.";

    public const string Prorated = "Salary prorated: the employee joined or left during this month.";
    public const string HighUnpaidLeave = "More than 5 unpaid leave days.";
    public const string ZeroNet = "Net salary is zero.";
    public const string StructureChangedMidMonth =
        "Salary structure changed during this month; the structure effective on the last day was used.";
    public const string NetChangedFromPreviousMonth =
        "Net salary differs from the previous month by more than 20%.";
    public const string IgnoredStatutoryStructure =
        "Statutory amounts on the salary structure were ignored; PF, ESI, PT, and LWF are calculated at payroll.";
    public const string StatutoryOverrideApplied = "A statutory amount was overridden for this run.";
    public const string EsiWagesAboveThreshold =
        "ESI wages are above the ₹21,000 eligibility band; contribution continues because the employee is covered.";
    public const string MissingGenderForProfessionalTax =
        "Gender is required for professional tax in this company state.";
    public const string WorkingDaysExceedCalendar =
        "Working days cannot exceed the number of calendar days in this month.";
    public const string UnpaidLeaveExceedsWorkingDays = "Unpaid leave cannot exceed working days.";
    public const string UnpaidLeaveExceedsDaysEmployed =
        "Unpaid leave cannot exceed days employed in this month.";
    public const string NegativeAttendance =
        "Present, paid leave, and unpaid leave cannot be negative.";
}
