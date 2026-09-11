"use client";

import { useEffect, useMemo, useRef, useState } from "react";
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
import { useToast } from "@/components/Toast";
import { Alert } from "@/components/ui/Alert";
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
  runStatusLabel,
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

function rowIdentityOk(row: DraftRow): boolean {
  const working = parseQuantity(row.workingDays) ?? 0;
  const present = parseQuantity(row.present) ?? 0;
  const paid = parseQuantity(row.paidLeave) ?? 0;
  const unpaid = parseQuantity(row.unpaidLeave) ?? 0;
  return attendanceBalances(working, present, paid, unpaid);
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
  const extrasEmployeeRef = useRef<DraftRow | null>(null);

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
  const missingStructureCount = useMemo(
    () => rows.filter((row) => !row.hasStructure).length,
    [rows],
  );
  const identityFailCount = useMemo(
    () => rows.filter((row) => !rowIdentityOk(row)).length,
    [rows],
  );
  const blockerCount = missingStructureCount + identityFailCount;
  const readiness = [
    missingStructureCount === 0
      ? "Salary structures complete"
      : `${missingStructureCount} missing salary structure`,
    identityFailCount === 0
      ? "Attendance balances"
      : `${identityFailCount} attendance identity`,
  ];
  const selectedLabel = periodLabel(year, month);

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

  const extrasEmployee = useMemo(() => {
    const current = rows.find((row) => row.employeeId === activeExtrasEmployeeId);
    if (current) {
      extrasEmployeeRef.current = current;
      return current;
    }
    return extrasEmployeeRef.current;
  }, [activeExtrasEmployeeId, rows]);

  return (
    <main className="sa-shell">
      <header className="sa-head sa-head--with-back">
        <h1>Monthly inputs</h1>
        <div className="sa-head__actions">
          <Link href={`/app/payroll/${year}/${month}/review`} className="sa-compose__secondary">
            Review
          </Link>
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
        </div>
        <p>
          {selectedLabel} attendance, overtime, bonuses, and deductions.
        </p>
      </header>

      <Alert>{error || null}</Alert>

      {calculated ? (
        <Alert tone="status">Saving will return this run to Draft so you can recalculate.</Alert>
      ) : null}
      {locked ? (
        <Alert tone="status">This payroll run is finalized or reversed and cannot be changed.</Alert>
      ) : null}

      {loading && !period ? (
        <div className="space-y-4 py-4" role="status">
          <div className="mp-kpi-grid">
            <Skeleton className="h-24 w-full" />
            <Skeleton className="h-24 w-full" />
            <Skeleton className="h-24 w-full" />
          </div>
          <Skeleton className="h-32 w-full" />
          <Skeleton className="h-12 w-full" />
          <Skeleton className="h-12 w-full" />
        </div>
      ) : !period?.run ? (
        <p className="sa-empty">
          No payroll run for this month.{" "}
          <Link href="/app/payroll">Start payroll</Link> first.
        </p>
      ) : (
        <>
          <div className="mp-kpi-grid" role="region" aria-label={`${selectedLabel} snapshot`}>
            <div className="mp-kpi-card">
              <span className="mp-kpi-card__label">Status</span>
              <span className="mp-kpi-card__value">{runStatusLabel(period.run.status)}</span>
              <span className="mp-kpi-card__subtext">{selectedLabel}</span>
            </div>
            <div className="mp-kpi-card">
              <span className="mp-kpi-card__label">Employees</span>
              <span className="mp-kpi-card__value">{rows.length}</span>
              <span className="mp-kpi-card__subtext">
                {period.workingDaysPerMonth} working days default
              </span>
            </div>
            <div className="mp-kpi-card">
              <span className="mp-kpi-card__label">Readiness</span>
              <span className="mp-kpi-card__value">
                {blockerCount === 0 ? "Clear" : `${blockerCount} missing`}
              </span>
              <span className="mp-kpi-card__subtext">{readiness.join(" · ")}</span>
            </div>
          </div>

          <section className="sa-payroll-card" aria-label={`${selectedLabel} inputs`}>
            <h2 className="sa-payroll-card__title">{selectedLabel}</h2>
            <p className="sa-payroll-card__lede">
              {locked
                ? "This run cannot be changed."
                : "Edit attendance, then save."}
            </p>
            <div className="sa-payroll-card__actions">
              <Button type="button" variant="secondary" onClick={applyWorkingDays} disabled={locked}>
                Apply working days
              </Button>
              <Button type="button" variant="secondary" onClick={markAllPresent} disabled={locked}>
                Mark all present
              </Button>
              <Button
                type="button"
                variant="secondary"
                onClick={autoBalanceUnpaidLeave}
                disabled={locked}
              >
                Balance unpaid leave
              </Button>
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
          </section>

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

          <PayrollExtrasDrawer
            open={Boolean(activeExtrasEmployeeId)}
            onClose={() => setActiveExtrasEmployeeId(null)}
            employeeName={extrasEmployee?.fullName ?? ""}
            employeeCode={extrasEmployee?.employeeCode ?? ""}
            locked={locked}
            initialOvertime={extrasEmployee?.overtime ?? []}
            initialBonuses={extrasEmployee?.bonuses ?? []}
            initialDeductions={extrasEmployee?.deductions ?? []}
            onSave={(overtime, bonuses, deductions) => {
              const employeeId = extrasEmployee?.employeeId;
              if (!employeeId) {
                return;
              }
              updateRow(employeeId, {
                overtime,
                bonuses,
                deductions,
              });
              toast.showSuccess(`Updated adjustments for ${extrasEmployee.fullName}.`);
            }}
          />
        </>
      )}
    </main>
  );
}
