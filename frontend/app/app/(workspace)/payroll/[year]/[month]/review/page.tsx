"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useParams } from "next/navigation";
import {
  PayrollRunStatus,
  calculatePayroll,
  getPayrollPeriod,
  type PayrollPeriodDetail,
} from "@/lib/api";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { formatRupees, periodLabel, runStatusLabel } from "@/lib/payroll";

export default function PayrollReviewPage() {
  const params = useParams<{ year: string; month: string }>();
  const year = Number(params.year);
  const month = Number(params.month);
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
            {locked ? (
              <p className="mp-group__hint">Finalized and reversed runs cannot be recalculated.</p>
            ) : null}
            <Link href={`/app/payroll/${year}/${month}`} className="sa-compose__secondary">
              Edit inputs
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
    </main>
  );
}
