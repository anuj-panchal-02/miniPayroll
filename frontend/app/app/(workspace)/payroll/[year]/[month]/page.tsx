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
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Select } from "@/components/ui/Select";
import {
  BONUS_TYPE_OPTIONS,
  DEDUCTION_TYPE_OPTIONS,
  attendanceBalances,
  parseQuantity,
  periodLabel,
} from "@/lib/payroll";

type ExtraLine = { type: string; amount: string; notes: string };
type OvertimeLine = { hours: string; rate: string; notes: string };

type DraftRow = {
  employeeId: string;
  employeeCode: string;
  fullName: string;
  hasStructure: boolean;
  workingDays: string;
  present: string;
  paidLeave: string;
  unpaidLeave: string;
  extrasOpen: boolean;
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
    extrasOpen:
      employee.overtime.length > 0 || employee.bonuses.length > 0 || employee.deductions.length > 0,
    overtime:
      employee.overtime.length > 0
        ? employee.overtime.map((item) => ({
            hours: String(item.hours),
            rate: item.rate != null ? String(item.rate) : employee.overtimeRate != null
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
  const attendance: PayrollInputsPayload["attendance"] = [];
  const overtime: PayrollInputsPayload["overtime"] = [];
  const bonuses: PayrollInputsPayload["bonuses"] = [];
  const deductions: PayrollInputsPayload["deductions"] = [];

  for (const row of rows) {
    const workingDays = parseQuantity(row.workingDays);
    const present = parseQuantity(row.present);
    const paidLeave = parseQuantity(row.paidLeave);
    const unpaidLeave = parseQuantity(row.unpaidLeave);
    if (workingDays == null || present == null || paidLeave == null || unpaidLeave == null) {
      return `Enter valid attendance for ${row.fullName}.`;
    }
    attendance.push({
      employeeId: row.employeeId,
      workingDays,
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
  const [saved, setSaved] = useState("");
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);

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

  const locked = period?.run?.status === PayrollRunStatus.Finalized
    || period?.run?.status === PayrollRunStatus.Reversed;
  const calculated = period?.run?.status === PayrollRunStatus.Calculated;
  const missingStructure = useMemo(
    () => rows.filter((row) => !row.hasStructure).map((row) => row.fullName),
    [rows],
  );

  function updateRow(employeeId: string, patch: Partial<DraftRow>) {
    setRows((current) =>
      current.map((row) => (row.employeeId === employeeId ? { ...row, ...patch } : row)),
    );
    setSaved("");
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
    setSaved("");
  }

  async function onSave() {
    if (!period?.run) return;
    const payload = buildPayload(rows);
    if (typeof payload === "string") {
      setError(payload);
      return;
    }
    setBusy(true);
    try {
      const next = await savePayrollInputs(period.run.id, payload);
      setPeriod(next);
      setRows(next.employees.map((employee) => toDraft(employee, next.workingDaysPerMonth)));
      setError("");
      setSaved("Monthly inputs saved.");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not save payroll inputs.");
    } finally {
      setBusy(false);
    }
  }

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
      <Alert tone="success">{saved || null}</Alert>
      {calculated ? (
        <Alert tone="status">Saving will return this run to Draft so you can recalculate.</Alert>
      ) : null}
      {locked ? (
        <Alert tone="status">This payroll run is finalized or reversed and cannot be changed.</Alert>
      ) : null}
      {loading ? (
        <p className="sa-empty" role="status">
          Loading monthly inputs…
        </p>
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
          <div className="sa-payroll-toolbar">
            <Button type="button" variant="secondary" onClick={applyWorkingDays} disabled={locked}>
              Apply {period.workingDaysPerMonth} working days to all
            </Button>
            <Link href={`/app/payroll/${year}/${month}/review`} className="sa-compose__secondary">
              Review payroll
            </Link>
          </div>
          <div className="sa-payroll-grid-wrap">
            <table className="sa-payroll-grid">
              <thead>
                <tr>
                  <th>Employee</th>
                  <th>Working</th>
                  <th>Present</th>
                  <th>Paid leave</th>
                  <th>Unpaid leave</th>
                  <th>One-time</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => {
                  const working = parseQuantity(row.workingDays) ?? 0;
                  const present = parseQuantity(row.present) ?? 0;
                  const paid = parseQuantity(row.paidLeave) ?? 0;
                  const unpaid = parseQuantity(row.unpaidLeave) ?? 0;
                  const identityOk = attendanceBalances(working, present, paid, unpaid);
                  return (
                    <tr key={row.employeeId} className={identityOk ? undefined : "sa-payroll-row--warn"}>
                      <td className="sa-payroll-grid__person">
                        <strong>{row.fullName}</strong>
                        <span>{row.employeeCode}</span>
                        {!row.hasStructure ? (
                          <p className="sa-payroll-warn">No salary structure</p>
                        ) : null}
                      </td>
                      <td>
                        <input
                          className="sa-payroll-qty"
                          type="number"
                          step="0.5"
                          min="0"
                          max="31"
                          aria-label={`Working days for ${row.fullName}`}
                          value={row.workingDays}
                          disabled={locked}
                          onChange={(event) => updateRow(row.employeeId, { workingDays: event.target.value })}
                        />
                      </td>
                      <td>
                        <input
                          className="sa-payroll-qty"
                          type="number"
                          step="0.5"
                          min="0"
                          max="31"
                          aria-label={`Present days for ${row.fullName}`}
                          value={row.present}
                          disabled={locked}
                          onChange={(event) => updateRow(row.employeeId, { present: event.target.value })}
                        />
                      </td>
                      <td>
                        <input
                          className="sa-payroll-qty"
                          type="number"
                          step="0.5"
                          min="0"
                          max="31"
                          aria-label={`Paid leave for ${row.fullName}`}
                          value={row.paidLeave}
                          disabled={locked}
                          onChange={(event) => updateRow(row.employeeId, { paidLeave: event.target.value })}
                        />
                      </td>
                      <td>
                        <input
                          className="sa-payroll-qty"
                          type="number"
                          step="0.5"
                          min="0"
                          max="31"
                          aria-label={`Unpaid leave for ${row.fullName}`}
                          value={row.unpaidLeave}
                          disabled={locked}
                          onChange={(event) => updateRow(row.employeeId, { unpaidLeave: event.target.value })}
                        />
                        {!identityOk ? (
                          <p className="sa-payroll-warn">Present + paid + unpaid must equal working days.</p>
                        ) : null}
                      </td>
                      <td>
                        <Button
                          type="button"
                          variant="ghost"
                          onClick={() => updateRow(row.employeeId, { extrasOpen: !row.extrasOpen })}
                        >
                          {row.extrasOpen ? "Hide extras" : "Overtime, bonus, deduction"}
                        </Button>
                        {row.extrasOpen ? (
                          <div className="sa-payroll-extras">
                            <p className="mp-group__hint">
                              Advances and loans are for this month only — no balance is tracked.
                            </p>
                            {row.overtime.map((item, index) => (
                              <div className="sa-payroll-extra-row" key={`ot-${index}`}>
                                <input
                                  className="sa-payroll-qty"
                                  type="number"
                                  step="0.25"
                                  min="0"
                                  aria-label={`Overtime hours for ${row.fullName}`}
                                  placeholder="Hours"
                                  value={item.hours}
                                  disabled={locked}
                                  onChange={(event) => {
                                    const overtime = row.overtime.slice();
                                    overtime[index] = { ...item, hours: event.target.value };
                                    updateRow(row.employeeId, { overtime });
                                  }}
                                />
                                <input
                                  className="sa-payroll-qty"
                                  type="number"
                                  step="0.01"
                                  min="0"
                                  aria-label={`Overtime rate for ${row.fullName}`}
                                  placeholder="Rate"
                                  value={item.rate}
                                  disabled={locked}
                                  onChange={(event) => {
                                    const overtime = row.overtime.slice();
                                    overtime[index] = { ...item, rate: event.target.value };
                                    updateRow(row.employeeId, { overtime });
                                  }}
                                />
                                <input
                                  className="mp-input"
                                  aria-label={`Overtime notes for ${row.fullName}`}
                                  placeholder="Notes"
                                  value={item.notes}
                                  disabled={locked}
                                  onChange={(event) => {
                                    const overtime = row.overtime.slice();
                                    overtime[index] = { ...item, notes: event.target.value };
                                    updateRow(row.employeeId, { overtime });
                                  }}
                                />
                                <Button
                                  type="button"
                                  variant="ghost"
                                  disabled={locked}
                                  onClick={() =>
                                    updateRow(row.employeeId, {
                                      overtime: row.overtime.filter((_, i) => i !== index),
                                    })
                                  }
                                >
                                  Remove
                                </Button>
                              </div>
                            ))}
                            <Button
                              type="button"
                              variant="secondary"
                              disabled={locked}
                              onClick={() =>
                                updateRow(row.employeeId, {
                                  overtime: [
                                    ...row.overtime,
                                    { hours: "", rate: "", notes: "" },
                                  ],
                                })
                              }
                            >
                              Add overtime
                            </Button>
                            {row.bonuses.map((item, index) => (
                              <div className="sa-payroll-extra-row" key={`bonus-${index}`}>
                                <Select
                                  id={`${row.employeeId}-bonus-type-${index}`}
                                  value={item.type}
                                  options={BONUS_TYPE_OPTIONS}
                                  disabled={locked}
                                  onChange={(value) => {
                                    const bonuses = row.bonuses.slice();
                                    bonuses[index] = { ...item, type: value };
                                    updateRow(row.employeeId, { bonuses });
                                  }}
                                />
                                <input
                                  className="sa-payroll-qty"
                                  type="number"
                                  step="0.01"
                                  min="0"
                                  aria-label={`Bonus amount for ${row.fullName}`}
                                  placeholder="Amount"
                                  value={item.amount}
                                  disabled={locked}
                                  onChange={(event) => {
                                    const bonuses = row.bonuses.slice();
                                    bonuses[index] = { ...item, amount: event.target.value };
                                    updateRow(row.employeeId, { bonuses });
                                  }}
                                />
                                <input
                                  className="mp-input"
                                  aria-label={`Bonus notes for ${row.fullName}`}
                                  placeholder="Notes"
                                  value={item.notes}
                                  disabled={locked}
                                  onChange={(event) => {
                                    const bonuses = row.bonuses.slice();
                                    bonuses[index] = { ...item, notes: event.target.value };
                                    updateRow(row.employeeId, { bonuses });
                                  }}
                                />
                                <Button
                                  type="button"
                                  variant="ghost"
                                  disabled={locked}
                                  onClick={() =>
                                    updateRow(row.employeeId, {
                                      bonuses: row.bonuses.filter((_, i) => i !== index),
                                    })
                                  }
                                >
                                  Remove
                                </Button>
                              </div>
                            ))}
                            <Button
                              type="button"
                              variant="secondary"
                              disabled={locked}
                              onClick={() =>
                                updateRow(row.employeeId, {
                                  bonuses: [
                                    ...row.bonuses,
                                    { type: String(BonusType.Festival), amount: "", notes: "" },
                                  ],
                                })
                              }
                            >
                              Add bonus
                            </Button>
                            {row.deductions.map((item, index) => (
                              <div className="sa-payroll-extra-row" key={`ded-${index}`}>
                                <Select
                                  id={`${row.employeeId}-deduction-type-${index}`}
                                  value={item.type}
                                  options={DEDUCTION_TYPE_OPTIONS}
                                  disabled={locked}
                                  onChange={(value) => {
                                    const deductions = row.deductions.slice();
                                    deductions[index] = { ...item, type: value };
                                    updateRow(row.employeeId, { deductions });
                                  }}
                                />
                                <input
                                  className="sa-payroll-qty"
                                  type="number"
                                  step="0.01"
                                  min="0"
                                  aria-label={`Deduction amount for ${row.fullName}`}
                                  placeholder="Amount"
                                  value={item.amount}
                                  disabled={locked}
                                  onChange={(event) => {
                                    const deductions = row.deductions.slice();
                                    deductions[index] = { ...item, amount: event.target.value };
                                    updateRow(row.employeeId, { deductions });
                                  }}
                                />
                                <input
                                  className="mp-input"
                                  aria-label={`Deduction notes for ${row.fullName}`}
                                  placeholder="Notes"
                                  value={item.notes}
                                  disabled={locked}
                                  onChange={(event) => {
                                    const deductions = row.deductions.slice();
                                    deductions[index] = { ...item, notes: event.target.value };
                                    updateRow(row.employeeId, { deductions });
                                  }}
                                />
                                <Button
                                  type="button"
                                  variant="ghost"
                                  disabled={locked}
                                  onClick={() =>
                                    updateRow(row.employeeId, {
                                      deductions: row.deductions.filter((_, i) => i !== index),
                                    })
                                  }
                                >
                                  Remove
                                </Button>
                              </div>
                            ))}
                            <Button
                              type="button"
                              variant="secondary"
                              disabled={locked}
                              onClick={() =>
                                updateRow(row.employeeId, {
                                  deductions: [
                                    ...row.deductions,
                                    {
                                      type: String(OneTimeDeductionType.AdvanceRecovery),
                                      amount: "",
                                      notes: "",
                                    },
                                  ],
                                })
                              }
                            >
                              Add deduction
                            </Button>
                          </div>
                        ) : null}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          <div className="sa-payroll-toolbar">
            <Button type="button" onClick={() => void onSave()} loading={busy} loadingLabel="Saving…" disabled={locked}>
              Save inputs
            </Button>
          </div>
        </>
      )}
    </main>
  );
}
