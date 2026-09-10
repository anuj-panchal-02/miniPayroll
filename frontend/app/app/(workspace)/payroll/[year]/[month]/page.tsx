"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import {
  BonusType,
  OneTimeDeductionType,
  PayrollRunStatus,
  getPayrollPeriod,
  savePayrollInputs,
  type PayrollInputsPayload,
  type PayrollPeriodDetail,
  type PayrollRosterEmployee,
} from "@/lib/api";
import { ToastOutlet, useToast } from "@/components/Toast";
import { Alert } from "@/components/ui/Alert";
import { Badge } from "@/components/ui/Badge";
import { Button } from "@/components/ui/Button";
import { Skeleton } from "@/components/ui/Skeleton";
import {
  PayrollExtrasDrawer,
  type ExtraLine,
  type OvertimeLine,
} from "@/components/PayrollExtrasDrawer";
import {
  attendanceBalances,
  parseQuantity,
  periodLabel,
} from "@/lib/payroll";

type DraftRow = {
  employeeId: string;
  employeeCode: string;
  fullName: string;
  hasStructure: boolean;
  workingDays: string;
  present: string;
  paidLeave: string;
  unpaidLeave: string;
  overtime: OvertimeLine[];
  bonuses: ExtraLine[];
  deductions: ExtraLine[];
};

function toDraft(employee: PayrollRosterEmployee, workingDaysPerMonth: number): DraftRow {
  const attendance = employee.attendance;
  const working = attendance?.workingDays ?? workingDaysPerMonth;
  return {
    employeeId: employee.employeeId,
    employeeCode: employee.employeeCode,
    fullName: employee.fullName,
    hasStructure: employee.hasStructure,
    workingDays: String(working),
    present: String(attendance?.present ?? working),
    paidLeave: String(attendance?.paidLeave ?? 0),
    unpaidLeave: String(attendance?.unpaidLeave ?? 0),
    overtime:
      employee.overtime.length > 0
        ? employee.overtime.map((item) => ({
            hours: String(item.hours),
            rate:
              item.rate != null
                ? String(item.rate)
                : employee.overtimeRate != null
                  ? String(employee.overtimeRate)
                  : "",
            notes: item.notes ?? "",
          }))
        : [],
    bonuses: employee.bonuses.map((item) => ({
      type: String(item.type),
      amount: String(item.amount),
      notes: item.notes ?? "",
    })),
    deductions: employee.deductions.map((item) => ({
      type: String(item.type),
      amount: String(item.amount),
      notes: item.notes ?? "",
    })),
  };
}

function buildPayload(rows: DraftRow[]): PayrollInputsPayload | string {
  const attendance = [];
  const overtime = [];
  const bonuses = [];
  const deductions = [];

  for (const row of rows) {
    const working = parseQuantity(row.workingDays);
    const present = parseQuantity(row.present);
    const paidLeave = parseQuantity(row.paidLeave);
    const unpaidLeave = parseQuantity(row.unpaidLeave);

    if (working == null || working <= 0) {
      return `Enter valid working days for ${row.fullName}.`;
    }
    if (present == null || present < 0) {
      return `Enter valid present days for ${row.fullName}.`;
    }
    if (paidLeave == null || paidLeave < 0) {
      return `Enter valid paid leave for ${row.fullName}.`;
    }
    if (unpaidLeave == null || unpaidLeave < 0) {
      return `Enter valid unpaid leave for ${row.fullName}.`;
    }
    if (!attendanceBalances(working, present, paidLeave, unpaidLeave)) {
      return `Attendance identity violated for ${row.fullName}.`;
    }

    attendance.push({
      employeeId: row.employeeId,
      workingDays: working,
      present,
      paidLeave,
      unpaidLeave,
    });

    for (const item of row.overtime) {
      if (!item.hours.trim()) continue;
      const hours = parseQuantity(item.hours);
      const rate = item.rate.trim() ? parseQuantity(item.rate) : null;
      if (hours == null || hours <= 0 || (rate != null && rate <= 0)) {
        return `Enter valid overtime for ${row.fullName}.`;
      }
      overtime.push({
        employeeId: row.employeeId,
        hours,
        rate,
        notes: item.notes.trim() || null,
      });
    }

    for (const item of row.bonuses) {
      if (!item.amount.trim()) continue;
      const amount = parseQuantity(item.amount);
      if (amount == null || amount <= 0) {
        return `Enter a valid bonus amount for ${row.fullName}.`;
      }
      bonuses.push({
        employeeId: row.employeeId,
        type: Number(item.type) as BonusType,
        amount,
        notes: item.notes.trim() || null,
      });
    }

    for (const item of row.deductions) {
      if (!item.amount.trim()) continue;
      const amount = parseQuantity(item.amount);
      if (amount == null || amount <= 0) {
        return `Enter a valid deduction amount for ${row.fullName}.`;
      }
      deductions.push({
        employeeId: row.employeeId,
        type: Number(item.type) as OneTimeDeductionType,
        amount,
        notes: item.notes.trim() || null,
      });
    }
  }

  return { attendance, overtime, bonuses, deductions };
}

export default function MonthlyInputsPage() {
  const params = useParams<{ year: string; month: string }>();
  const year = Number(params.year);
  const month = Number(params.month);
  const [period, setPeriod] = useState<PayrollPeriodDetail | null>(null);
  const [rows, setRows] = useState<DraftRow[]>([]);
  const [error, setError] = useState("");
  const toast = useToast();
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [activeExtrasEmployeeId, setActiveExtrasEmployeeId] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    getPayrollPeriod(year, month)
      .then((payload) => {
        if (cancelled) return;
        setPeriod(payload);
        setRows(payload.employees.map((employee) => toDraft(employee, payload.workingDaysPerMonth)));
        setError("");
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
  const calculated = period?.run?.status === PayrollRunStatus.Calculated;
  const missingStructure = useMemo(
    () => rows.filter((row) => !row.hasStructure).map((row) => row.fullName),
    [rows],
  );

  function updateRow(employeeId: string, patch: Partial<DraftRow>) {
    setRows((current) =>
      current.map((row) => (row.employeeId === employeeId ? { ...row, ...patch } : row)),
    );
  }

  function applyWorkingDays() {
    if (!period) return;
    const working = String(period.workingDaysPerMonth);
    setRows((current) =>
      current.map((row) => ({
        ...row,
        workingDays: working,
        present: working,
        paidLeave: "0",
        unpaidLeave: "0",
      })),
    );
    toast.showSuccess(`Applied ${working} working days to all employees.`);
  }

  function markAllPresent() {
    if (!period) return;
    setRows((current) =>
      current.map((row) => ({
        ...row,
        present: row.workingDays,
        paidLeave: "0",
        unpaidLeave: "0",
      })),
    );
    toast.showSuccess("Marked all employees 100% present.");
  }

  function autoBalanceUnpaidLeave() {
    if (!period) return;
    setRows((current) =>
      current.map((row) => {
        const working = parseQuantity(row.workingDays) ?? 0;
        const present = parseQuantity(row.present) ?? 0;
        const paid = parseQuantity(row.paidLeave) ?? 0;
        const accounted = present + paid;
        if (accounted < working) {
          return {
            ...row,
            unpaidLeave: String(working - accounted),
          };
        }
        return row;
      }),
    );
    toast.showSuccess("Auto-balanced unpaid leave for short attendance.");
  }

  async function onSave() {
    if (!period?.run) return;
    const payload = buildPayload(rows);
    if (typeof payload === "string") {
      toast.showError(payload);
      return;
    }
    setBusy(true);
    try {
      const next = await savePayrollInputs(period.run.id, payload);
      setPeriod(next);
      setRows(next.employees.map((employee) => toDraft(employee, next.workingDaysPerMonth)));
      toast.showSuccess("Monthly inputs saved.");
    } catch (reason) {
      toast.showError(reason instanceof Error ? reason.message : "Could not save payroll inputs.");
    } finally {
      setBusy(false);
    }
  }

  const activeEmployee = useMemo(
    () => rows.find((r) => r.employeeId === activeExtrasEmployeeId) ?? null,
    [rows, activeExtrasEmployeeId],
  );

  return (
    <main className="sa-shell">
      <header className="sa-head sa-head--with-back">
        <Link href="/app/payroll" className="sa-back" aria-label="Payroll">
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
        <h1>Monthly inputs</h1>
        <p>{periodLabel(year, month)} attendance, overtime, bonuses, and deductions.</p>
      </header>

      <Alert>{error || null}</Alert>
      <ToastOutlet toast={toast} />

      {calculated ? (
        <Alert tone="status">Saving will return this run to Draft so you can recalculate.</Alert>
      ) : null}
      {locked ? (
        <Alert tone="status">This payroll run is finalized or reversed and cannot be changed.</Alert>
      ) : null}

      {loading ? (
        <div className="space-y-4 py-8">
          <p className="sa-empty" role="status">
            Loading monthly inputs…
          </p>
          <div className="space-y-2">
            <Skeleton className="h-10 w-full" />
            <Skeleton className="h-12 w-full" />
            <Skeleton className="h-12 w-full" />
          </div>
        </div>
      ) : !period?.run ? (
        <p className="sa-empty">
          No payroll run for this month.{" "}
          <Link href="/app/payroll">Start payroll</Link> first.
        </p>
      ) : (
        <>
          {missingStructure.length > 0 ? (
            <Alert tone="status">
              {`Missing salary structure: ${missingStructure.join(", ")}.`}
            </Alert>
          ) : null}

          {/* Quick Toolbar */}
          <div className="sa-payroll-toolbar flex flex-wrap items-center justify-between gap-3">
            <div className="flex flex-wrap items-center gap-2">
              <Button
                type="button"
                variant="secondary"
                onClick={applyWorkingDays}
                disabled={locked}
              >
                Apply {period.workingDaysPerMonth} working days to all
              </Button>
              <Button
                type="button"
                variant="secondary"
                onClick={markAllPresent}
                disabled={locked}
              >
                Mark all 100% present
              </Button>
              <Button
                type="button"
                variant="secondary"
                onClick={autoBalanceUnpaidLeave}
                disabled={locked}
              >
                Auto-balance unpaid leave
              </Button>
            </div>
            <Link href={`/app/payroll/${year}/${month}/review`} className="sa-compose__secondary">
              Review payroll →
            </Link>
          </div>

          {/* Clean Grid Table */}
          <div className="sa-payroll-grid-wrap">
            <table className="sa-payroll-grid">
              <thead>
                <tr>
                  <th>Employee</th>
                  <th className="sa-payroll-grid__num">Working</th>
                  <th className="sa-payroll-grid__num">Present</th>
                  <th className="sa-payroll-grid__num">Paid leave</th>
                  <th className="sa-payroll-grid__num">Unpaid leave</th>
                  <th className="sa-payroll-grid__center">Status / Balance</th>
                  <th className="sa-payroll-grid__action">Adjustments</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => {
                  const working = parseQuantity(row.workingDays) ?? 0;
                  const present = parseQuantity(row.present) ?? 0;
                  const paid = parseQuantity(row.paidLeave) ?? 0;
                  const unpaid = parseQuantity(row.unpaidLeave) ?? 0;
                  const totalDays = present + paid + unpaid;
                  const delta = totalDays - working;
                  const identityOk = attendanceBalances(working, present, paid, unpaid);
                  const totalExtras =
                    row.overtime.length + row.bonuses.length + row.deductions.length;

                  return (
                    <tr
                      key={row.employeeId}
                      className={identityOk ? undefined : "sa-payroll-row--warn"}
                    >
                      <td className="sa-payroll-grid__person">
                        <strong>{row.fullName}</strong>
                        <span>{row.employeeCode}</span>
                        {!row.hasStructure ? (
                          <p className="sa-payroll-warn">No salary structure</p>
                        ) : null}
                      </td>
                      <td className="sa-payroll-grid__num">
                        <input
                          className="sa-payroll-qty"
                          type="number"
                          step="0.5"
                          min="0"
                          max="31"
                          aria-label={`Working days for ${row.fullName}`}
                          value={row.workingDays}
                          disabled={locked}
                          onChange={(event) =>
                            updateRow(row.employeeId, { workingDays: event.target.value })
                          }
                        />
                      </td>
                      <td className="sa-payroll-grid__num">
                        <input
                          className="sa-payroll-qty"
                          type="number"
                          step="0.5"
                          min="0"
                          max="31"
                          aria-label={`Present days for ${row.fullName}`}
                          value={row.present}
                          disabled={locked}
                          onChange={(event) =>
                            updateRow(row.employeeId, { present: event.target.value })
                          }
                        />
                      </td>
                      <td className="sa-payroll-grid__num">
                        <input
                          className="sa-payroll-qty"
                          type="number"
                          step="0.5"
                          min="0"
                          max="31"
                          aria-label={`Paid leave for ${row.fullName}`}
                          value={row.paidLeave}
                          disabled={locked}
                          onChange={(event) =>
                            updateRow(row.employeeId, { paidLeave: event.target.value })
                          }
                        />
                      </td>
                      <td className="sa-payroll-grid__num">
                        <input
                          className="sa-payroll-qty"
                          type="number"
                          step="0.5"
                          min="0"
                          max="31"
                          aria-label={`Unpaid leave for ${row.fullName}`}
                          value={row.unpaidLeave}
                          disabled={locked}
                          onChange={(event) =>
                            updateRow(row.employeeId, { unpaidLeave: event.target.value })
                          }
                        />
                        {!identityOk ? (
                          <p className="sa-payroll-warn text-xs mt-1">
                            Present + paid + unpaid must equal working days.
                          </p>
                        ) : null}
                      </td>
                      <td className="sa-payroll-grid__center">
                        {identityOk ? (
                          <span className="mp-balance-pill mp-balance-pill--balanced">
                            ✓ {totalDays}/{working}d
                          </span>
                        ) : (
                          <span className="mp-balance-pill mp-balance-pill--unbalanced">
                            ⚠ {totalDays}/{working}d ({delta > 0 ? `+${delta}` : delta}d)
                          </span>
                        )}
                      </td>
                      <td className="sa-payroll-grid__action">
                        <Button
                          type="button"
                          variant="secondary"
                          onClick={() => setActiveExtrasEmployeeId(row.employeeId)}
                        >
                          {totalExtras > 0 ? `Extras (${totalExtras})` : "Add Extras"}
                        </Button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>

          <div className="sa-payroll-toolbar">
            <Button
              type="button"
              onClick={() => void onSave()}
              loading={busy}
              loadingLabel="Saving…"
              disabled={locked}
            >
              Save inputs
            </Button>
          </div>

          {/* Slide-over Extras Drawer */}
          {activeEmployee ? (
            <PayrollExtrasDrawer
              open={Boolean(activeExtrasEmployeeId)}
              onClose={() => setActiveExtrasEmployeeId(null)}
              employeeName={activeEmployee.fullName}
              employeeCode={activeEmployee.employeeCode}
              locked={locked}
              initialOvertime={activeEmployee.overtime}
              initialBonuses={activeEmployee.bonuses}
              initialDeductions={activeEmployee.deductions}
              onSave={(overtime, bonuses, deductions) => {
                updateRow(activeEmployee.employeeId, {
                  overtime,
                  bonuses,
                  deductions,
                });
                toast.showSuccess(`Updated adjustments for ${activeEmployee.fullName}.`);
              }}
            />
          ) : null}
        </>
      )}
    </main>
  );
}
