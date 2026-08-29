namespace MiniPayroll.Domain.Constants;

public static class TableNames
{
    public const string Prefix = "mp_";

    public const string User = "mp_TblUser";
    public const string Role = "mp_TblRole";
    public const string UserRole = "mp_TblUserRole";
    public const string UserClaim = "mp_TblUserClaim";
    public const string RoleClaim = "mp_TblRoleClaim";
    public const string UserLogin = "mp_TblUserLogin";
    public const string UserToken = "mp_TblUserToken";

    public const string Company = "mp_TblCompany";
    public const string Plan = "mp_TblPlan";
    public const string Subscription = "mp_TblSubscription";
    public const string Payment = "mp_TblPayment";
    public const string AuditLog = "mp_TblAuditLog";
    public const string Employee = "mp_TblEmployee";
    public const string SalaryComponent = "mp_TblSalaryComponent";
    public const string SalaryStructure = "mp_TblSalaryStructure";
    public const string EmployeeSalaryComponent = "mp_TblEmployeeSalaryComponent";

    public static readonly IReadOnlyList<string> All =
    [
        User, Role, UserRole, UserClaim, RoleClaim, UserLogin, UserToken,
        Company, Plan, Subscription, Payment, AuditLog, Employee, SalaryComponent,
        SalaryStructure, EmployeeSalaryComponent
    ];
}
