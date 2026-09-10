"use client";

import { useEffect, useMemo, useState } from "react";
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
  updatePayrollPayment,
  type PayrollEmployeeDetail,
  type PayrollPeriodDetail,
} from "@/lib/api";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { ConfirmDialog } from "@/components/ui/ConfirmDialog";
import { DateField } from "@/components/ui/DateField";
import { Field } from "@/components/ui/Field";
import { Select } from "@/components/ui/Select";
import { formatRupees, periodLabel, runStatusLabel } from "@/lib/payroll";

export default function PayrollReviewPage() {
  const params = useParams<{ year: string; month: string }>();
  const year = Number(params.year);
  const month = Number(params.month);
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [period, setPeriod] = useState<PayrollPeriodDetail | null>(null);
  const [error, setError] = useState("");
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

  const locked = period?.run?.status === PayrollRunStatus.Finalized
    || period?.run?.status === PayrollRunStatus.Reversed;
  const canFinalize = period?.run?.status === PayrollRunStatus.Calculated
    && (period.totals?.errorCount ?? 0) === 0
    && (period.results?.length ?? 0) > 0;
  const canDownload = period?.run?.status === PayrollRunStatus.Finalized
    || period?.run?.status === PayrollRunStatus.Reversed;
  const canPay = period?.run?.status === PayrollRunStatus.Finalized;
  const stale =
    period?.run?.status === PayrollRunStatus.Draft
    && (period.results?.length ?? 0) > 0
    && period.results.every((item) => item.errors.length === 0);
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
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not calculate payroll.");
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
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not finalize payroll.");
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
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not download payslips.");
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
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not download payslip.");
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
      setError("");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not update payment.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <main className="sa-shell">
      <header className="sa-head sa-head--with-back">
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
        <h1>Payroll review</h1>
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
      {loading ? (
        <p className="sa-empty" role="status">
          Loading review…
        </p>
      ) : !period?.run ? (
        <p className="sa-empty">
          No payroll run for this month.{" "}
          <Link href="/app/payroll">Choose a period</Link>.
        </p>
      ) : (
        <>
          <div className="sa-payroll-toolbar">
            <Button
              type="button"
              onClick={() => void onCalculate()}
              loading={busy}
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
            {locked ? (
              <p className="mp-group__hint">Finalized and reversed runs cannot be recalculated.</p>
            ) : null}
            <Link href={`/app/payroll/${year}/${month}`} className="sa-compose__secondary">
              Edit inputs
            </Link>
            <Link href="/app/payroll/history" className="sa-compose__secondary">
              History
            </Link>
          </div>
          {period.results.length === 0 ? (
            <p className="sa-empty">Calculate payroll to see gross, deductions, and net by employee.</p>
          ) : (
            <section className="sa-payroll-review" aria-label="Payroll results">
              {ordered.map((employee) => (
                <article key={employee.employeeId} className="sa-payroll-review__row">
                  <div className="sa-payroll-review__head">
                    <div className="sa-payroll-grid__person">
                      <strong>{employee.fullName}</strong>
                      <span>{employee.employeeCode}</span>
                      {employee.errors.length > 0 ? (
                        <p className="sa-payroll-warn">{employee.errors.join(" ")}</p>
                      ) : null}
                      {employee.warnings.length > 0 ? (
                        <p className="mp-group__hint">{employee.warnings.join(" ")}</p>
                      ) : null}
                      {canDownload ? (
                        <p className="mp-group__hint">
                          {employee.paymentStatus === SalaryPaymentStatus.Paid ? "Paid" : "Unpaid"}
                        </p>
                      ) : null}
                    </div>
                    <div className="sa-payroll-review__amounts">
                      <span>{formatRupees(employee.grossEarnings)}</span>
                      <span>{formatRupees(employee.totalDeductions)}</span>
                      <span>{formatRupees(employee.netSalary)}</span>
                    </div>
                  </div>
                  <Button
                    type="button"
                    variant="ghost"
                    onClick={() =>
                      setOpenId((current) =>
                        current === employee.employeeId ? null : employee.employeeId,
                      )
                    }
                  >
                    {openId === employee.employeeId ? "Hide breakdown" : "Show breakdown"}
                  </Button>
                  {canDownload ? (
                    <Button
                      type="button"
                      variant="ghost"
                      onClick={() => void onDownload(employee.employeeId)}
                      disabled={busy}
                    >
                      Download payslip
                    </Button>
                  ) : null}
                  {canPay ? (
                    <PaymentEditor
                      employee={employee}
                      disabled={busy}
                      onSave={(details) => void onPay(employee.employeeId, true, details)}
                      onClear={() => void onPay(employee.employeeId, false)}
                    />
                  ) : null}
                  {openId === employee.employeeId ? (
                    <ul className="sa-payroll-review__lines">
                      {employee.earnings.map((line) => (
                        <li key={`e-${line.sortOrder}`}>
                          {line.name} {formatRupees(line.amount)}
                        </li>
                      ))}
                      {employee.deductions.map((line) => (
                        <li key={`d-${line.sortOrder}`}>
                          {line.name} −{formatRupees(line.amount)}
                        </li>
                      ))}
                    </ul>
                  ) : null}
                </article>
              ))}
              {period.totals ? (
                <p className="sa-payroll-totals">
                  <span>
                    {period.totals.employeeCount} paid · {period.totals.warningCount} warnings ·{" "}
                    {period.totals.errorCount} errors
                  </span>
                  <span>{formatRupees(period.totals.grossEarnings)}</span>
                  <span>{formatRupees(period.totals.totalDeductions)}</span>
                  <span>{formatRupees(period.totals.netSalary)}</span>
                </p>
              ) : null}
            </section>
          )}
        </>
      )}
      <ConfirmDialog
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        title="Finalize payroll"
        description="Finalizing locks this month. Amounts cannot be changed after this."
        confirmLabel="Finalize"
        onConfirm={() => void onFinalize()}
        confirmLoading={busy}
        confirmLoadingLabel="Finalizing…"
      />
    </main>
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

  return (
    <div className="sa-payroll-card__actions">
      <Field id={`pay-mode-${employee.employeeId}`} label="Mode">
        <Select
          value={String(mode)}
          options={[
            { value: String(SalaryPaymentMode.Bank), label: "Bank" },
            { value: String(SalaryPaymentMode.Upi), label: "UPI" },
            { value: String(SalaryPaymentMode.Cash), label: "Cash" },
          ]}
          onChange={(next) => setMode(Number(next) as SalaryPaymentMode)}
          disabled={disabled}
        />
      </Field>
      <Field id={`pay-on-${employee.employeeId}`} label="Paid on">
        <DateField value={paidOn} onChange={setPaidOn} disabled={disabled} />
      </Field>
      <Field id={`pay-ref-${employee.employeeId}`} label="Reference">
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
          variant="ghost"
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
