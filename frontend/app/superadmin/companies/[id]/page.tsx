"use client";

import { FormEvent, useEffect, useId, useRef, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useToast } from "@/components/Toast";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Dialog } from "@/components/ui/Dialog";
import { DateField } from "@/components/ui/DateField";
import { Field } from "@/components/ui/Field";
import { FieldGroup } from "@/components/ui/FieldGroup";
import { PasswordField } from "@/components/ui/PasswordField";
import { Select } from "@/components/ui/Select";
import {
  BillableSource,
  CompanyDetail,
  CreateAdminResponse,
  PayrollRunStatus,
  activateCompany,
  createCompanyAdmin,
  getCompany,
  getCompanyBilling,
  getPlatformLimits,
  listCompanyPayrollRuns,
  recordCompanyPayment,
  reversePayrollRun,
  setToken,
  updateCompanyLimit,
  type CompanyBilling,
  type PayrollHistoryItem,
} from "@/lib/api";
import { FALLBACK_PLATFORM_LIMITS } from "@/lib/platform";
import { formatRupees, periodLabel, runStatusLabel, billingFormula, formatDueDate } from "@/lib/payroll";
import { emailError, employeeLimitError } from "@/lib/validation";
import { ConfirmDialog } from "@/components/ui/ConfirmDialog";

const ADMIN_EMAIL_MESSAGES = {
  empty: "Enter an admin email.",
  invalid: "Enter a valid admin email.",
};

const PAYMENT_MODES = [
  { value: "UPI", label: "UPI" },
  { value: "NEFT", label: "NEFT" },
  { value: "Cash", label: "Cash" },
];

function todayIsoDate() {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, "0")}-${String(now.getDate()).padStart(2, "0")}`;
}

function billableSourceLabel(source: BillableSource) {
  return source === BillableSource.FinalizedPayroll
    ? "Finalized payroll"
    : "Headcount";
}

export default function CompanyDetailsPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const id = params.id;
  const dialogRef = useRef<HTMLDialogElement>(null);
  const limitRef = useRef<HTMLInputElement>(null);
  const adminEmailRef = useRef<HTMLInputElement>(null);
  const adminPasswordRef = useRef<HTMLInputElement>(null);
  const titleId = useId();
  const copyId = useId();

  const [company, setCompany] = useState<CompanyDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const toast = useToast();
  const [adminEmail, setAdminEmail] = useState("");
  const [adminPassword, setAdminPassword] = useState("");
  const [busy, setBusy] = useState(false);
  const [activating, setActivating] = useState(false);
  const [loading, setLoading] = useState(true);
  const [credentials, setCredentials] = useState<CreateAdminResponse | null>(null);
  const [employeeLimit, setEmployeeLimit] = useState("");
  const [limits, setLimits] = useState(FALLBACK_PLATFORM_LIMITS);
  const [savingLimit, setSavingLimit] = useState(false);
  const [limitTouched, setLimitTouched] = useState(false);
  const [adminEmailTouched, setAdminEmailTouched] = useState(false);
  const [adminPasswordTouched, setAdminPasswordTouched] = useState(false);
  const [payrollRuns, setPayrollRuns] = useState<PayrollHistoryItem[]>([]);
  const [reversalReason, setReversalReason] = useState("");
  const [reversingId, setReversingId] = useState<string | null>(null);
  const [reverseOpen, setReverseOpen] = useState(false);
  const [pendingReverseId, setPendingReverseId] = useState<string | null>(null);
  const [billing, setBilling] = useState<CompanyBilling | null>(null);
  const [paymentPeriod, setPaymentPeriod] = useState("");
  const [paymentAmount, setPaymentAmount] = useState("");
  const [paymentDate, setPaymentDate] = useState(todayIsoDate);
  const [paymentMode, setPaymentMode] = useState("UPI");
  const [invoiceGst, setInvoiceGst] = useState("");
  const [recordingPayment, setRecordingPayment] = useState(false);

  const limitFieldError = employeeLimitError(employeeLimit, {
    min: limits.minEmployeeLimit,
    max: limits.hardEmployeeCap,
  });
  const adminEmailFieldError = emailError(adminEmail, ADMIN_EMAIL_MESSAGES);
  const adminPasswordFieldError = adminPassword.trim()
    ? null
    : "Enter a temporary password.";
  const shownLimitError = limitTouched ? limitFieldError : null;
  const shownAdminEmailError = adminEmailTouched ? adminEmailFieldError : null;
  const shownAdminPasswordError = adminPasswordTouched ? adminPasswordFieldError : null;

  function applyBilling(next: CompanyBilling) {
    setBilling(next);
    const preferred =
      next.periods.find((period) => period.remaining > 0) ?? next.periods.at(-1) ?? null;
    if (!preferred) {
      setPaymentPeriod("");
      setPaymentAmount("");
      return;
    }
    setPaymentPeriod(preferred.billingPeriod);
    setPaymentAmount(preferred.remaining > 0 ? String(preferred.remaining) : "");
  }

  useEffect(() => {
    let cancelled = false;
    async function load() {
      try {
        const [detail, loadedLimits, runs, loadedBilling] = await Promise.all([
          getCompany(id),
          getPlatformLimits().catch(() => FALLBACK_PLATFORM_LIMITS),
          listCompanyPayrollRuns(id).catch(() => []),
          getCompanyBilling(id).catch(() => null),
        ]);
        if (cancelled) {
          return;
        }
        setLimits(loadedLimits);
        setCompany(detail);
        setPayrollRuns(runs);
        if (loadedBilling) {
          applyBilling(loadedBilling);
        }
        setAdminEmail(detail.adminEmail ?? detail.contactEmail);
        setEmployeeLimit(String(detail.employeeLimit));
        setError(null);
      } catch (err) {
        if (cancelled) {
          return;
        }
        setError(err instanceof Error ? err.message : "Could not load company");
        if (String(err).toLowerCase().includes("unauthorized")) {
          setToken(null);
          router.push("/login");
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void load();
    return () => {
      cancelled = true;
    };
  }, [id, router]);

  async function onCreateAdmin(event: FormEvent) {
    event.preventDefault();
    setAdminEmailTouched(true);
    setAdminPasswordTouched(true);
    if (adminEmailFieldError) {
      adminEmailRef.current?.focus();
      return;
    }
    if (adminPasswordFieldError) {
      adminPasswordRef.current?.focus();
      return;
    }
    setBusy(true);
    toast.dismiss();
    try {
      const created = await createCompanyAdmin(id, adminEmail, adminPassword);
      setCredentials(created);
      setAdminPassword("");
      setCompany((current) =>
        current
          ? { ...current, hasAdmin: true, adminEmail: created.email }
          : current,
      );
    } catch (err) {
      toast.showError(err instanceof Error ? err.message : "Could not create Company Admin");
    } finally {
      setBusy(false);
    }
  }

  function openActivateConfirm() {
    dialogRef.current?.showModal();
  }

  function closeActivateConfirm() {
    dialogRef.current?.close();
  }

  async function confirmActivate() {
    closeActivateConfirm();
    setActivating(true);
    toast.dismiss();
    try {
      const result = await activateCompany(id);
      setCompany(result);
      applyBilling(await getCompanyBilling(id));
      toast.showSuccess("Company activated.");
    } catch (err) {
      toast.showError(err instanceof Error ? err.message : "Could not activate company");
      if (String(err).toLowerCase().includes("unauthorized")) {
        setToken(null);
        router.push("/login");
      }
    } finally {
      setActivating(false);
    }
  }

  async function confirmReverse() {
    if (!pendingReverseId) {
      return;
    }
    const reason = reversalReason.trim();
    if (!reason) {
      return;
    }
    const runId = pendingReverseId;
    setReverseOpen(false);
    setPendingReverseId(null);
    setReversingId(runId);
    toast.dismiss();
    try {
      await reversePayrollRun(id, runId, reason);
      setPayrollRuns(await listCompanyPayrollRuns(id));
      toast.showSuccess("Payroll reversed.");
    } catch (err) {
      toast.showError(err instanceof Error ? err.message : "Could not reverse payroll.");
    } finally {
      setReversingId(null);
    }
  }

  async function onSaveLimit(event: FormEvent) {
    event.preventDefault();
    setLimitTouched(true);
    if (limitFieldError) {
      limitRef.current?.focus();
      return;
    }
    setSavingLimit(true);
    toast.dismiss();
    try {
      const result = await updateCompanyLimit(id, Number(employeeLimit));
      setCompany(result);
      setEmployeeLimit(String(result.employeeLimit));
      toast.showSuccess("Employee limit saved.");
    } catch (err) {
      toast.showError(err instanceof Error ? err.message : "Could not update employee limit");
      if (String(err).toLowerCase().includes("unauthorized")) {
        setToken(null);
        router.push("/login");
      }
    } finally {
      setSavingLimit(false);
    }
  }

  async function onRecordPayment(event: FormEvent) {
    event.preventDefault();
    const amount = Number(paymentAmount);
    if (!paymentPeriod || !Number.isFinite(amount) || amount <= 0 || !paymentDate) {
      toast.showError("Enter a period, amount, and payment date.");
      return;
    }
    setRecordingPayment(true);
    toast.dismiss();
    try {
      applyBilling(
        await recordCompanyPayment(id, {
          billingPeriod: paymentPeriod,
          amount,
          paidOn: `${paymentDate}T00:00:00.000Z`,
          paymentMode,
          invoiceGstReference: invoiceGst.trim() || null,
        }),
      );
      setInvoiceGst("");
      toast.showSuccess("Payment recorded.");
    } catch (err) {
      toast.showError(err instanceof Error ? err.message : "Could not record payment.");
    } finally {
      setRecordingPayment(false);
    }
  }

  const canActivate =
    company?.hasAdmin === true && company.status === "Pending";

  const intro = !company
    ? null
    : company.status === "Active"
      ? "Company is active. The Company Admin can sign in."
      : company.hasAdmin
        ? "Company Admin is set. Activate remains the next step."
        : "Create the one Company Admin for this company.";

  const heading = company?.name ?? "Company";
  const subhead = loading
    ? "Loading company details."
    : company
      ? intro
      : "This company was not found.";

  return (
    <main className="sa-shell">
        <header className="sa-head sa-head--with-back">
          <Link href="/superadmin" className="sa-back" aria-label="Companies">
            <svg className="sa-back__icon" viewBox="0 0 24 24" aria-hidden="true">
              <path
                fill="none"
                stroke="currentColor"
                strokeWidth="2"
                strokeLinecap="round"
                strokeLinejoin="round"
                d="M19 12H5m7 7-7-7 7-7"
              />
            </svg>
          </Link>
          <h1>{heading}</h1>
          <p>{subhead}</p>
        </header>
        <Alert>{error}</Alert>

        {company ? (
          <>
            <dl className="sa-facts">
              <div>
                <dt>Contact</dt>
                <dd>{company.contactEmail}</dd>
              </div>
              <div>
                <dt>Status</dt>
                <dd>
                  <span className="sa-chip" data-status={company.status}>
                    {company.status}
                  </span>
                </dd>
              </div>
              <div>
                <dt>Plan</dt>
                <dd>{company.planName}</dd>
              </div>
              <div>
                <dt>Admin</dt>
                <dd>{company.adminEmail ?? "Not created"}</dd>
              </div>
            </dl>

            <form className="sa-compose sa-compose--single" noValidate autoComplete="off" onSubmit={onSaveLimit}>
              <Field
                id="employee-limit"
                label="Employee limit"
                hint={`Up to ${limits.hardEmployeeCap} employees.`}
                error={shownLimitError}
                required
              >
                <input
                  ref={limitRef}
                  className="mp-input"
                  name="employeeLimit"
                  type="number"
                  min={limits.minEmployeeLimit}
                  max={limits.hardEmployeeCap}
                  value={employeeLimit}
                  onChange={(e) => {
                    setEmployeeLimit(e.target.value);
                  }}
                  onBlur={() => setLimitTouched(true)}
                  disabled={savingLimit}
                />
              </Field>
              <Button type="submit" loading={savingLimit} loadingLabel="Saving">
                Save employee limit
              </Button>
            </form>

            {company.activatedAt && billing && billing.periods.length > 0 ? (
              <FieldGroup
                title="Billing"
                hint="Collection is offline. Record UPI, NEFT, or cash against a calendar month. GST invoices stay outside the product."
              >
                <div className="sa-master-wrap">
                  <table className="sa-master">
                    <thead>
                      <tr>
                        <th scope="col">Period</th>
                        <th scope="col">Charge</th>
                        <th scope="col">Due date</th>
                        <th scope="col">Paid</th>
                        <th scope="col">Remaining</th>
                      </tr>
                    </thead>
                    <tbody>
                      {billing.periods.map((period) => (
                        <tr key={period.billingPeriod}>
                          <td>
                            {periodLabel(period.year, period.month)}
                            {period.isEstimated ? (
                              <>
                                {" "}
                                <span className="sa-chip">Estimated</span>
                              </>
                            ) : null}
                          </td>
                          <td>
                            {billingFormula(
                              period.billableEmployees,
                              period.pricePerEmployee,
                              period.amountDue,
                            )}
                            <span className="mp-group__hint">
                              {" "}
                              {billableSourceLabel(period.billableSource)}
                            </span>
                          </td>
                          <td>{formatDueDate(period.dueDate)}</td>
                          <td>{formatRupees(period.paidAmount)}</td>
                          <td>{formatRupees(period.remaining)}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                </div>

                <form className="sa-compose" noValidate autoComplete="off" onSubmit={onRecordPayment}>
                  <Field id="billing-period" label="Period" required>
                    <Select
                      value={paymentPeriod}
                      options={billing.periods.map((period) => ({
                        value: period.billingPeriod,
                        label: periodLabel(period.year, period.month),
                      }))}
                      onChange={(next) => {
                        setPaymentPeriod(next);
                        const selected = billing.periods.find((period) => period.billingPeriod === next);
                        if (selected && selected.remaining > 0) {
                          setPaymentAmount(String(selected.remaining));
                        }
                      }}
                    />
                  </Field>
                  <Field id="billing-amount" label="Amount" required>
                    <input
                      className="mp-input"
                      name="amount"
                      type="number"
                      min="0.01"
                      step="0.01"
                      value={paymentAmount}
                      onChange={(event) => setPaymentAmount(event.target.value)}
                      disabled={recordingPayment}
                    />
                  </Field>
                  <Field id="billing-date" label="Payment date" required>
                    <DateField
                      name="paidOn"
                      value={paymentDate}
                      onChange={setPaymentDate}
                      disabled={recordingPayment}
                    />
                  </Field>
                  <Field id="billing-mode" label="Mode" required>
                    <Select
                      value={paymentMode}
                      options={PAYMENT_MODES}
                      onChange={setPaymentMode}
                    />
                  </Field>
                  <Field id="billing-gst" label="GST / invoice reference" optional>
                    <input
                      className="mp-input"
                      name="invoiceGstReference"
                      value={invoiceGst}
                      onChange={(event) => setInvoiceGst(event.target.value)}
                      disabled={recordingPayment}
                    />
                  </Field>
                  <Button type="submit" loading={recordingPayment} loadingLabel="Recording">
                    Record payment
                  </Button>
                </form>
              </FieldGroup>
            ) : null}

            {credentials ? (
              <section className="sa-secret" aria-live="polite">
                <h2>Company Admin created</h2>
                <p>
                  Company Admin created for {credentials.email}. Share the
                  password you entered out of band. They must change it at first
                  login.
                </p>
              </section>
            ) : company.hasAdmin ? null : (
              <form className="sa-compose sa-compose--single" noValidate autoComplete="off" onSubmit={onCreateAdmin}>
                <FieldGroup title="Company Admin" className="sa-compose__span">
                  <Field
                    id="admin-email"
                    label="Admin email"
                    error={shownAdminEmailError}
                    required
                  >
                    <input
                      ref={adminEmailRef}
                      className="mp-input"
                      name="email"
                      type="email"
                      value={adminEmail}
                      onChange={(e) => setAdminEmail(e.target.value)}
                      onBlur={() => setAdminEmailTouched(true)}
                      disabled={busy}
                    />
                  </Field>
                  <PasswordField
                    id="admin-password"
                    label="Temporary password"
                    name="temporaryPassword"
                    inputRef={adminPasswordRef}
                    value={adminPassword}
                    onChange={(e) => setAdminPassword(e.target.value)}
                    onBlur={() => setAdminPasswordTouched(true)}
                    disabled={busy}
                    autoComplete="new-password"
                    required
                    hint="Type a temporary password to share out of band. It is never shown again."
                    error={shownAdminPasswordError}
                  />
                </FieldGroup>
                <Button type="submit" loading={busy} loadingLabel="Creating">
                  Create admin
                </Button>
              </form>
            )}

            {canActivate ? (
              <>
                <div className="sa-activate">
                  <Button
                    type="button"
                    loading={activating}
                    loadingLabel="Activating"
                    aria-haspopup="dialog"
                    onClick={openActivateConfirm}
                  >
                    Activate company
                  </Button>
                </div>

                <Dialog
                  ref={dialogRef}
                  className="sa-dialog"
                  title="Activate company"
                  description="This starts the billing period. The Company Admin can then sign in."
                  titleId={titleId}
                  descriptionId={copyId}
                  onBackdropClick={closeActivateConfirm}
                >
                  <button
                    type="button"
                    className="sa-dialog__confirm"
                    onClick={() => void confirmActivate()}
                  >
                    Activate
                  </button>
                  <button type="submit" className="sa-dialog__cancel">
                    Cancel
                  </button>
                </Dialog>
              </>
            ) : null}
          </>
        ) : null}

        {company && payrollRuns.length > 0 ? (
          <FieldGroup title="Payroll runs">
            <p className="mp-group__hint">
              Reversal is Superadmin-only and needs a reason. Amounts stay on the reversed run.
            </p>
            <Field id="reversal-reason" label="Reversal reason" required>
              <textarea
                className="mp-textarea"
                rows={2}
                value={reversalReason}
                onChange={(event) => setReversalReason(event.target.value)}
              />
            </Field>
            <div className="sa-master-wrap">
              <table className="sa-master">
                <thead>
                  <tr>
                    <th scope="col">Period</th>
                    <th scope="col">Status</th>
                    <th scope="col">Employees</th>
                    <th scope="col">Net</th>
                    <th scope="col">Actions</th>
                  </tr>
                </thead>
                <tbody>
                  {payrollRuns.map((run) => (
                    <tr key={run.id}>
                      <td>{periodLabel(run.year, run.month)}</td>
                      <td>
                        <span className="sa-chip">{runStatusLabel(run.status)}</span>
                      </td>
                      <td>{run.employeeCount} employees</td>
                      <td>{formatRupees(run.netSalary)}</td>
                      <td>
                        {run.status === PayrollRunStatus.Finalized ? (
                          <div className="sa-master__actions">
                            <Button
                              type="button"
                              variant="ghost"
                              loading={reversingId === run.id}
                              loadingLabel="Reversing…"
                              disabled={!reversalReason.trim()}
                              onClick={() => {
                                setPendingReverseId(run.id);
                                setReverseOpen(true);
                              }}
                            >
                              Reverse
                            </Button>
                          </div>
                        ) : null}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <ConfirmDialog
              open={reverseOpen}
              onOpenChange={(open) => {
                setReverseOpen(open);
                if (!open) {
                  setPendingReverseId(null);
                }
              }}
              title="Reverse payroll"
              description="Reversal is Superadmin-only and cannot be undone. Amounts stay on the reversed run."
              confirmLabel="Reverse"
              tone="destructive"
              confirmLoading={pendingReverseId !== null && reversingId === pendingReverseId}
              confirmLoadingLabel="Reversing…"
              onConfirm={() => void confirmReverse()}
            />
          </FieldGroup>
        ) : null}
    </main>
  );
}
