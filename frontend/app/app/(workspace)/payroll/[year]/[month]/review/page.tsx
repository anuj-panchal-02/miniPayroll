"use client";

import { Fragment, useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import {
  PayrollRunStatus,
  SalaryPaymentMode,
  SalaryPaymentStatus,
  calculatePayroll,
  downloadAllPayrollPayslips,
  downloadPayrollPayslip,
  finalizePayroll,
  getPayrollPeriod,
  setStatutoryOverrides,
  updatePayrollPayment,
  type PayrollEmployeeDetail,
  type PayrollLineDetail,
  type PayrollPeriodDetail,
  type StatutoryKind,
} from "@/lib/api";
import { useToast } from "@/components/Toast";
import { Alert } from "@/components/ui/Alert";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { ConfirmDialog } from "@/components/ui/ConfirmDialog";
import { DateField } from "@/components/ui/DateField";
import { Field } from "@/components/ui/Field";
import { Select } from "@/components/ui/Select";
import { Skeleton } from "@/components/ui/Skeleton";
import { formatRupees, periodLabel, runStatusLabel, statutoryAppliedLabel } from "@/lib/payroll";
import { Button as UiButton } from "@/components/shadcn/button";
import {
  Table,
  TableBody,
  TableCell,
  TableFooter,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/shadcn/table";

export default function PayrollReviewPage() {
  const params = useParams<{ year: string; month: string }>();
  const year = Number(params.year);
  const month = Number(params.month);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [period, setPeriod] = useState<PayrollPeriodDetail | null>(null);
  const [error, setError] = useState("");
  const toast = useToast();
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [openId, setOpenId] = useState<string | null>(null);

  async function load() {
    const payload = await getPayrollPeriod(year, month);
    setPeriod(payload);
    setError("");
  }

  useEffect(() => {
    let cancelled = false;
    getPayrollPeriod(year, month)
      .then((payload) => {
        if (!cancelled) {
          setPeriod(payload);
          setError("");
        }
      })
      .catch((reason) => {
        if (!cancelled) {
          setError(reason instanceof Error ? reason.message : "Could not load payroll.");
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [year, month]);

  const locked =
    period?.run?.status === PayrollRunStatus.Finalized ||
    period?.run?.status === PayrollRunStatus.Reversed;
  const canFinalize =
    period?.run?.status === PayrollRunStatus.Calculated &&
    (period.totals?.errorCount ?? 0) === 0 &&
    (period.results?.length ?? 0) > 0;
  const canDownload =
    period?.run?.status === PayrollRunStatus.Finalized ||
    period?.run?.status === PayrollRunStatus.Reversed;
  const canPay = period?.run?.status === PayrollRunStatus.Finalized;
  const stale =
    period?.run?.status === PayrollRunStatus.Draft &&
    (period.results?.length ?? 0) > 0 &&
    period.results.every((item) => item.errors.length === 0);

  const ordered = useMemo(() => {
    const results = period?.results ?? [];
    return [...results].sort((left, right) => {
      if (left.errors.length !== right.errors.length) {
        return right.errors.length - left.errors.length;
      }
      return left.employeeCode.localeCompare(right.employeeCode);
    });
  }, [period]);

  async function onCalculate() {
    setBusy(true);
    try {
      await calculatePayroll(year, month);
      await load();
      toast.showSuccess("Payroll calculated.");
    } catch (reason) {
      toast.showError(reason instanceof Error ? reason.message : "Could not calculate payroll.");
    } finally {
      setBusy(false);
    }
  }

  async function onFinalize() {
    if (!period?.run) {
      return;
    }
    setConfirmOpen(false);
    setBusy(true);
    try {
      await finalizePayroll(period.run.id);
      await load();
      toast.showSuccess("Payroll finalized.");
    } catch (reason) {
      toast.showError(reason instanceof Error ? reason.message : "Could not finalize payroll.");
    } finally {
      setBusy(false);
    }
  }

  async function onDownloadAll() {
    if (!period?.run) {
      return;
    }
    setBusy(true);
    try {
      await downloadAllPayrollPayslips(period.run.id);
      toast.showSuccess("Payslips downloaded.");
    } catch (reason) {
      toast.showError(reason instanceof Error ? reason.message : "Could not download payslips.");
    } finally {
      setBusy(false);
    }
  }

  async function onDownload(employeeId: string) {
    if (!period?.run) {
      return;
    }
    setBusy(true);
    try {
      await downloadPayrollPayslip(period.run.id, employeeId);
      toast.showSuccess("Payslip downloaded.");
    } catch (reason) {
      toast.showError(reason instanceof Error ? reason.message : "Could not download payslip.");
    } finally {
      setBusy(false);
    }
  }

  async function onPay(
    employeeId: string,
    paid: boolean,
    details?: { mode: SalaryPaymentMode; paidOn: string; reference: string },
  ) {
    if (!period?.run) {
      return;
    }
    setBusy(true);
    try {
      const next = await updatePayrollPayment(period.run.id, employeeId, {
        paymentStatus: paid ? SalaryPaymentStatus.Paid : SalaryPaymentStatus.Unpaid,
        paymentMode: paid ? (details?.mode ?? SalaryPaymentMode.Bank) : null,
        paidOn: paid ? (details?.paidOn ?? new Date().toISOString().slice(0, 10)) : null,
        paymentReference: paid ? (details?.reference || null) : null,
      });
      setPeriod(next);
      toast.showSuccess("Payment updated.");
    } catch (reason) {
      toast.showError(reason instanceof Error ? reason.message : "Could not update payment.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <main className="sa-shell">
      <header className="sa-head sa-head--with-back">
        <h1>Payroll review</h1>
        <div className="sa-head__actions">
          <Link href="/app/payroll/history" className="sa-compose__secondary">
            History
          </Link>
          <Link href={`/app/payroll/${year}/${month}`} className="sa-back" aria-label="Monthly inputs">
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
        </div>
        <p>
          {periodLabel(year, month)}
          {period?.run ? ` · ${runStatusLabel(period.run.status)}` : ""}.
        </p>
      </header>

      <Alert>{error || null}</Alert>

      {stale ? (
        <Alert tone="status">
          Inputs changed after the last calculation. Recalculate to refresh these figures.
        </Alert>
      ) : null}
      {period?.run?.status === PayrollRunStatus.Finalized ? (
        <Alert tone="status">This payroll run is finalized. Figures are locked.</Alert>
      ) : null}
      {period?.run?.status === PayrollRunStatus.Reversed ? (
        <Alert tone="status">This payroll run was reversed. Payslips stay available with a watermark.</Alert>
      ) : null}

      {loading && !period ? (
        <div className="space-y-4 py-4" role="status">
          <div className="mp-kpi-grid">
            <Skeleton className="h-24 w-full" />
            <Skeleton className="h-24 w-full" />
            <Skeleton className="h-24 w-full" />
            <Skeleton className="h-24 w-full" />
          </div>
          <Skeleton className="h-32 w-full" />
        </div>
      ) : !period?.run ? (
        <p className="sa-empty">
          No payroll run for this month.{" "}
          <Link href="/app/payroll">Choose a period</Link>.
        </p>
      ) : (
        <>
          {period.totals ? (
            <div className="mp-kpi-grid" role="region" aria-label={`${periodLabel(year, month)} snapshot`}>
              <div className="mp-kpi-card">
                <span className="mp-kpi-card__label">Gross</span>
                <span className="mp-kpi-card__value">{formatRupees(period.totals.grossEarnings)}</span>
                <span className="mp-kpi-card__subtext">All employees this month</span>
              </div>
              <div className="mp-kpi-card">
                <span className="mp-kpi-card__label">Deductions</span>
                <span className="mp-kpi-card__value">{formatRupees(period.totals.totalDeductions)}</span>
                <span className="mp-kpi-card__subtext">Recurring and one-time</span>
              </div>
              <div className="mp-kpi-card">
                <span className="mp-kpi-card__label">Net</span>
                <span className="mp-kpi-card__value">{formatRupees(period.totals.netSalary)}</span>
                <span className="mp-kpi-card__subtext">Amount to pay</span>
              </div>
              <div className="mp-kpi-card">
                <span className="mp-kpi-card__label">Status</span>
                <span className="mp-kpi-card__value">{runStatusLabel(period.run.status)}</span>
                <span className="mp-kpi-card__subtext">
                  {period.totals.employeeCount}{" "}
                  {period.totals.employeeCount === 1 ? "employee" : "employees"}
                  {period.totals.errorCount > 0
                    ? ` · ${period.totals.errorCount} ${period.totals.errorCount === 1 ? "error" : "errors"}`
                    : ""}
                  {period.totals.warningCount > 0
                    ? ` · ${period.totals.warningCount} ${period.totals.warningCount === 1 ? "warning" : "warnings"}`
                    : ""}
                </span>
              </div>
            </div>
          ) : null}

          <section className="sa-payroll-card" aria-label={`${periodLabel(year, month)} review`}>
            <h2 className="sa-payroll-card__title">{periodLabel(year, month)}</h2>
            <p className="sa-payroll-card__lede">
              {locked
                ? "This run is locked."
                : canFinalize
                  ? "Figures look complete. Finalize to lock them."
                  : "Calculate to refresh gross, deductions, and net."}
            </p>
            <div className="sa-payroll-card__actions">
              <Button
                type="button"
                variant="secondary"
                onClick={() => void onCalculate()}
                loading={busy && !canDownload}
                loadingLabel="Calculating…"
                disabled={locked}
              >
                {period.results.length > 0 ? "Recalculate" : "Calculate"}
              </Button>
              {canFinalize ? (
                <Button type="button" onClick={() => setConfirmOpen(true)} disabled={busy}>
                  Finalize
                </Button>
              ) : null}
              {canDownload ? (
                <Button type="button" onClick={() => void onDownloadAll()} disabled={busy}>
                  Download all
                </Button>
              ) : null}
              <Link href={`/app/payroll/${year}/${month}`} className="sa-compose__secondary">
                Edit inputs
              </Link>
            </div>
          </section>

          {period.results.length === 0 ? (
            <p className="sa-empty">Calculate payroll to see gross, deductions, and net by employee.</p>
          ) : (
            <Table className="sa-payroll-review mt-6" aria-label="Payroll results">
              <TableHeader>
                <TableRow>
                  <TableHead className="text-left">Employee</TableHead>
                  <TableHead className="sa-payroll-review__num text-right">Gross</TableHead>
                  <TableHead className="sa-payroll-review__num text-right">Deductions</TableHead>
                  <TableHead className="sa-payroll-review__num text-right">Net</TableHead>
                  <TableHead className="text-right">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {ordered.map((employee) => {
                  const open = openId === employee.employeeId;
                  const detail = canPay || open;
                  return (
                    <Fragment key={employee.employeeId}>
                      <TableRow className="hover:bg-paper-2 transition-colors">
                        <TableCell className="sa-payroll-grid__person text-left align-middle">
                          <strong>{employee.fullName}</strong>
                          <span>{employee.employeeCode}</span>
                          {employee.errors.length > 0 ? (
                            <p className="sa-payroll-warn">{employee.errors.join(" ")}</p>
                          ) : null}
                          {employee.warnings.length > 0 ? (
                            <p className="mp-group__hint">{employee.warnings.join(" ")}</p>
                          ) : null}
                          {canDownload ? (
                            <div className="mt-1">
                              <Badge
                                tone={
                                  employee.paymentStatus === SalaryPaymentStatus.Paid
                                    ? "success"
                                    : "neutral"
                                }
                                size="sm"
                              >
                                {employee.paymentStatus === SalaryPaymentStatus.Paid
                                  ? "Paid"
                                  : "Unpaid"}
                              </Badge>
                            </div>
                          ) : null}
                        </TableCell>
                        <TableCell className="sa-payroll-review__num text-right font-mono align-middle">
                          {formatRupees(employee.grossEarnings)}
                        </TableCell>
                        <TableCell className="sa-payroll-review__num text-right font-mono align-middle">
                          {formatRupees(employee.totalDeductions)}
                        </TableCell>
                        <TableCell className="sa-payroll-review__num text-right font-mono font-semibold align-middle">
                          {formatRupees(employee.netSalary)}
                        </TableCell>
                        <TableCell className="text-right align-middle">
                          <div className="flex flex-wrap items-center justify-end gap-1">
                            <UiButton
                              type="button"
                              size="sm"
                              variant="ghost"
                              onClick={() =>
                                setOpenId((current) =>
                                  current === employee.employeeId ? null : employee.employeeId,
                                )
                              }
                            >
                              {open ? "Hide breakdown" : "Show breakdown"}
                            </UiButton>
                            {canDownload ? (
                              <UiButton
                                type="button"
                                size="sm"
                                variant="secondary"
                                onClick={() => void onDownload(employee.employeeId)}
                              >
                                Download payslip
                              </UiButton>
                            ) : null}
                          </div>
                        </TableCell>
                      </TableRow>
                      {detail ? (
                        <TableRow className="sa-payroll-detail">
                          <TableCell colSpan={5}>
                            <div className="sa-payroll-detail__wrap">
                              {open ? (
                                <div className="sa-payroll-detail__columns">
                                  <div>
                                    <h4 className="font-semibold text-xs uppercase tracking-wider text-muted-foreground mb-2">Earnings</h4>
                                    <ul className="space-y-1.5">
                                      {employee.earnings.map((earning, index) => (
                                        <li key={`earn-${index}`} className="flex justify-between items-center text-xs py-0.5 border-b border-rule">
                                          <span>{earning.name}</span>
                                          <span className="font-mono font-medium">{formatRupees(earning.amount)}</span>
                                        </li>
                                      ))}
                                    </ul>
                                  </div>
                                  <div>
                                    <h4 className="font-semibold text-xs uppercase tracking-wider text-muted-foreground mb-2">Deductions</h4>
                                    <StatutoryDeductionList
                                      employee={employee}
                                      runId={period.run.id}
                                      locked={locked}
                                      busy={busy}
                                      onSaved={load}
                                    />
                                  </div>
                                </div>
                              ) : null}
                              {canPay ? (
                                <PaymentEditor
                                  employee={employee}
                                  disabled={busy}
                                  onSave={(details) => void onPay(employee.employeeId, true, details)}
                                  onClear={() => void onPay(employee.employeeId, false)}
                                />
                              ) : null}
                            </div>
                          </TableCell>
                        </TableRow>
                      ) : null}
                    </Fragment>
                  );
                })}
              </TableBody>
              {period.totals ? (
                <TableFooter>
                  <TableRow>
                    <TableCell className="text-left align-middle">
                      <strong>Total</strong>
                      <span className="mp-group__hint block text-xs">
                        {period.totals.employeeCount} paid · {period.totals.warningCount} warnings ·{" "}
                        {period.totals.errorCount} errors
                      </span>
                    </TableCell>
                    <TableCell className="sa-payroll-review__num text-right font-mono align-middle">
                      {formatRupees(period.totals.grossEarnings)}
                    </TableCell>
                    <TableCell className="sa-payroll-review__num text-right font-mono align-middle">
                      {formatRupees(period.totals.totalDeductions)}
                    </TableCell>
                    <TableCell className="sa-payroll-review__num text-right font-mono font-bold align-middle">
                      {formatRupees(period.totals.netSalary)}
                    </TableCell>
                    <TableCell className="text-right" />
                  </TableRow>
                </TableFooter>
              ) : null}
            </Table>
          )}
        </>
      )}

      <ConfirmDialog
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        title="Finalize payroll"
        description="Finalizing snapshots every employee, earning, deduction, and payslip. Once finalized, amounts cannot be changed."
        confirmLabel="Finalize"
        onConfirm={() => void onFinalize()}
        confirmLoading={busy}
        confirmLoadingLabel="Finalizing…"
      />
    </main>
  );
}

function statutoryLines(employee: PayrollEmployeeDetail): PayrollLineDetail[] {
  return employee.deductions.filter((line) => line.statutoryKind != null);
}

function otherDeductions(employee: PayrollEmployeeDetail): PayrollLineDetail[] {
  return employee.deductions.filter((line) => line.statutoryKind == null);
}

function draftAmountsFrom(employee: PayrollEmployeeDetail): Record<string, string> {
  return Object.fromEntries(
    statutoryLines(employee).map((line) => [String(line.statutoryKind), String(line.amount)]),
  );
}

function StatutoryDeductionList({
  employee,
  runId,
  locked,
  busy,
  onSaved,
}: {
  employee: PayrollEmployeeDetail;
  runId: string;
  locked: boolean;
  busy: boolean;
  onSaved: () => Promise<void>;
}) {
  const toast = useToast();
  const statutory = statutoryLines(employee);
  const extras = otherDeductions(employee);
  const [drafts, setDrafts] = useState(() => draftAmountsFrom(employee));
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    setDrafts(draftAmountsFrom(employee));
  }, [employee]);

  const dirty = statutory.some((line) => {
    const next = Number(drafts[String(line.statutoryKind)]);
    return Number.isFinite(next) && next !== line.amount;
  });

  async function onApply() {
    const overrides: Array<{ kind: StatutoryKind; amount: number }> = [];
    for (const line of statutory) {
      const kind = line.statutoryKind;
      if (kind == null) continue;
      const amount = Number(drafts[String(kind)]);
      if (!Number.isFinite(amount) || amount < 0) {
        toast.showError("Statutory amounts must be zero or more.");
        return;
      }
      const computed = line.computedAmount ?? line.amount;
      if (amount !== computed) {
        overrides.push({ kind, amount });
      }
    }
    setSaving(true);
    try {
      await setStatutoryOverrides(runId, employee.employeeId, overrides);
      await onSaved();
      toast.showSuccess("Statutory amounts updated.");
    } catch (reason) {
      toast.showError(
        reason instanceof Error ? reason.message : "Could not update statutory amounts.",
      );
    } finally {
      setSaving(false);
    }
  }

  return (
    <>
      <ul className="space-y-1.5">
        {extras.map((deduction, index) => (
          <li
            key={`ded-${index}`}
            className="flex justify-between items-center text-xs py-0.5 border-b border-rule"
          >
            <span>{deduction.name}</span>
            <span className="font-mono font-medium">{formatRupees(deduction.amount)}</span>
          </li>
        ))}
        {statutory.map((deduction) => {
          const kind = deduction.statutoryKind!;
          const computed = deduction.computedAmount ?? deduction.amount;
          const applied = Number(drafts[String(kind)] ?? deduction.amount);
          const overridden = Number.isFinite(applied) && applied !== computed;
          return (
            <li key={`stat-${kind}`} className="text-xs py-0.5 border-b border-rule space-y-1">
              <div className="flex justify-between items-center gap-2">
                <span>{deduction.name}</span>
                <span className="font-mono font-medium">
                  {statutoryAppliedLabel(
                    Number.isFinite(applied) ? applied : deduction.amount,
                    computed,
                  )}
                </span>
              </div>
              {overridden ? (
                <p className="sa-payroll-warn">Override differs from the computed amount.</p>
              ) : null}
              {!locked ? (
                <Field
                  id={`statutory-${employee.employeeId}-${kind}`}
                  label={`Override ${deduction.name}`}
                >
                  <input
                    className="mp-input"
                    type="number"
                    min="0"
                    step="1"
                    inputMode="numeric"
                    value={drafts[String(kind)] ?? ""}
                    disabled={busy || saving}
                    onChange={(event) =>
                      setDrafts((current) => ({
                        ...current,
                        [String(kind)]: event.target.value,
                      }))
                    }
                  />
                </Field>
              ) : null}
            </li>
          );
        })}
      </ul>
      {employee.employerPf || employee.employerEsi ? (
        <p className="mp-group__hint mt-2">
          Employer PF {formatRupees(employee.employerPf ?? 0)} · Employer ESI{" "}
          {formatRupees(employee.employerEsi ?? 0)}
        </p>
      ) : null}
      {!locked && statutory.length > 0 ? (
        <div className="mt-2">
          <Button
            type="button"
            variant="secondary"
            onClick={() => void onApply()}
            loading={saving}
            loadingLabel="Saving…"
            disabled={busy || !dirty}
          >
            Apply statutory overrides
          </Button>
        </div>
      ) : null}
    </>
  );
}

function PaymentEditor({
  employee,
  disabled,
  onSave,
  onClear,
}: {
  employee: PayrollEmployeeDetail;
  disabled: boolean;
  onSave: (details: { mode: SalaryPaymentMode; paidOn: string; reference: string }) => void;
  onClear: () => void;
}) {
  const [mode, setMode] = useState<SalaryPaymentMode>(
    employee.paymentMode ?? SalaryPaymentMode.Bank,
  );
  const [paidOn, setPaidOn] = useState(
    employee.paidOn ?? new Date().toISOString().slice(0, 10),
  );
  const [reference, setReference] = useState(employee.paymentReference ?? "");
  const [unpaidOpen, setUnpaidOpen] = useState(false);
  const modeId = `pay-mode-${employee.employeeId}`;
  const paidId = `pay-on-${employee.employeeId}`;
  const refId = `pay-ref-${employee.employeeId}`;

  return (
    <div className="sa-payroll-review__pay">
      <Field id={modeId} label="Mode">
        <Select
          value={String(mode)}
          onChange={(next) => setMode(Number(next) as SalaryPaymentMode)}
          options={[
            { value: String(SalaryPaymentMode.Bank), label: "Bank" },
            { value: String(SalaryPaymentMode.Upi), label: "UPI" },
            { value: String(SalaryPaymentMode.Cash), label: "Cash" },
          ]}
          disabled={disabled}
        />
      </Field>
      <Field id={paidId} label="Paid on">
        <DateField value={paidOn} onChange={setPaidOn} disabled={disabled} />
      </Field>
      <Field id={refId} label="Reference">
        <input
          className="mp-input"
          value={reference}
          onChange={(event) => setReference(event.target.value)}
          disabled={disabled}
        />
      </Field>
      {employee.paymentStatus === SalaryPaymentStatus.Paid ? (
        <Button type="button" variant="ghost" onClick={() => setUnpaidOpen(true)} disabled={disabled}>
          Mark unpaid
        </Button>
      ) : (
        <Button
          type="button"
          variant="secondary"
          onClick={() => onSave({ mode, paidOn, reference })}
          disabled={disabled || !paidOn}
        >
          Mark paid
        </Button>
      )}
      <ConfirmDialog
        open={unpaidOpen}
        onOpenChange={setUnpaidOpen}
        title="Mark unpaid"
        description="This clears the paid date, mode, and reference for this employee."
        confirmLabel="Mark unpaid"
        tone="destructive"
        onConfirm={() => {
          setUnpaidOpen(false);
          onClear();
        }}
      />
    </div>
  );
}
