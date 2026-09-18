namespace MiniPayroll.Domain.Constants;

public static class AuditActions
{
    public const string CompanySetupComplete = "company.setup.complete";
    public const string EmployeeCreate = "employee.create";
    public const string EmployeeUpdate = "employee.update";
    public const string EmployeeDeactivate = "employee.deactivate";
    public const string SalaryStructureCreate = "salary-structure.create";
    public const string PayrollRunCreate = "payroll.run.create";
    public const string PayrollRunCalculate = "payroll.run.calculate";
    public const string PayrollInputsSave = "payroll.inputs.save";
    public const string PayrollRunFinalize = "payroll.run.finalize";
    public const string PayrollRunReverse = "payroll.run.reverse";
    public const string PayrollPaymentUpdate = "payroll.payment.update";
    public const string PayrollStatutoryOverride = "payroll.statutory.override";
    public const string BillingPaymentRecord = "billing.payment.record";
    public const string InvoiceCreate = "invoice.create";
    public const string InvoiceIssue = "invoice.issue";
    public const string InvoicePayment = "invoice.payment";
    public const string InvoiceVoid = "invoice.void";
    public const string InvoiceFail = "invoice.fail";
    public const string InvoiceRefund = "invoice.refund";
    public const string InvoicePaymentPending = "invoice.payment_pending";
    public const string PlanPriceChange = "plan.price.change";
    public const string CompanyActivate = "company.activate";
    public const string SubscriptionPastDue = "subscription.past_due";
    public const string SubscriptionGrace = "subscription.grace";
    public const string SubscriptionSuspend = "subscription.suspend";
    public const string SubscriptionCancel = "subscription.cancel";
    public const string SubscriptionExpire = "subscription.expire";
    public const string SubscriptionReactivate = "subscription.reactivate";
    public const string SubscriptionPlanChange = "subscription.plan.change";
    public const string PaymentVerified = "payment.verified";
}
