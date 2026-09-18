namespace MiniPayroll.Domain.Subscriptions;

public static class PlanFeatureCodes
{
    public const string Payroll = "PAYROLL";
    public const string Payslips = "PAYSLIPS";
    public const string Attendance = "ATTENDANCE";
    public const string Overtime = "OVERTIME";
    public const string Bonus = "BONUS";
    public const string Advances = "ADVANCES";
    public const string Loans = "LOANS";
    public const string StatutoryPayroll = "STATUTORY_PAYROLL";
    public const string Reports = "REPORTS";
    public const string AuditLog = "AUDIT_LOG";
    public const string MultipleAdmins = "MULTIPLE_ADMINS";
    public const string EmployeeSelfService = "EMPLOYEE_SELF_SERVICE";
    public const string ApiAccess = "API_ACCESS";

    public static readonly IReadOnlyList<string> CoreEnabled =
    [
        Payroll,
        Payslips,
        Attendance,
        Overtime,
        Bonus,
        StatutoryPayroll,
        Reports,
        AuditLog
    ];
}
