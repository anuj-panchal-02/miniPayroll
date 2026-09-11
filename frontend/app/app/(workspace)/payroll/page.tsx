"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  getPayrollPeriod,
  createPayrollRun,
  type PayrollPeriodDetail,
} from "@/lib/api";
import { useToast } from "@/components/Toast";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Field } from "@/components/ui/Field";
import { FieldGroup } from "@/components/ui/FieldGroup";
import { Select } from "@/components/ui/Select";
import { Skeleton } from "@/components/ui/Skeleton";
import { MONTH_LABELS, periodLabel, runStatusLabel } from "@/lib/payroll";

function currentPeriod() {
  const now = new Date();
  return { year: now.getFullYear(), month: now.getMonth() + 1 };
}

function readinessLines(
  missingStructure: number,
  missingAttendance: number,
  hasRun: boolean,
): string[] {
  const lines: string[] = [];
  lines.push(
    missingStructure === 0
      ? "Salary structures complete"
      : `${missingStructure} missing salary structure`,
  );
  if (hasRun) {
    lines.push(
      missingAttendance === 0
        ? "Attendance complete"
        : `${missingAttendance} missing attendance`,
    );
  }
  return lines;
}

export default function PayrollPage() {
  const router = useRouter();
  const initial = currentPeriod();
  const [year, setYear] = useState(initial.year);
  const [month, setMonth] = useState(initial.month);
  const [period, setPeriod] = useState<PayrollPeriodDetail | null>(null);
  const [error, setError] = useState("");
  const toast = useToast();
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);

  const years = useMemo(() => {
    const current = new Date().getFullYear();
    return [current - 1, current, current + 1];
  }, []);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
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
          setPeriod(null);
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [year, month]);

  const missingStructure = period?.employees.filter((employee) => !employee.hasStructure).length ?? 0;
  const missingAttendance =
    period?.employees.filter((employee) => employee.attendance === null).length ?? 0;
  const blockerCount = missingStructure + (period?.run ? missingAttendance : 0);
  const readiness = period
    ? readinessLines(missingStructure, missingAttendance, Boolean(period.run))
    : [];

  async function onStart() {
    setBusy(true);
    try {
      await createPayrollRun(year, month);
      router.push(`/app/payroll/${year}/${month}`);
    } catch (reason) {
      toast.showError(reason instanceof Error ? reason.message : "Could not start payroll.");
    } finally {
      setBusy(false);
    }
  }

  const selectedLabel = periodLabel(year, month);

  return (
    <main className="sa-shell">
      <header className="sa-head sa-head--with-back">
        <h1>Payroll</h1>
        <Link href="/app/payroll/history" className="sa-compose__secondary">
          History
        </Link>
        <p>Prepare monthly inputs, calculate, review, and look up closed months.</p>
      </header>
      <FieldGroup title="Payroll month">
        <Field id="payroll-month" label="Month">
          <Select
            value={String(month)}
            options={MONTH_LABELS.map((label, index) => ({
              value: String(index + 1),
              label,
            }))}
            onChange={(value) => setMonth(Number(value))}
          />
        </Field>
        <Field id="payroll-year" label="Year">
          <Select
            value={String(year)}
            options={years.map((item) => ({
              value: String(item),
              label: String(item),
            }))}
            onChange={(value) => setYear(Number(value))}
          />
        </Field>
      </FieldGroup>
      <Alert>{error || null}</Alert>
      {loading && !period ? (
        <div className="space-y-4 py-4" role="status">
          <div className="mp-kpi-grid">
            <Skeleton className="h-24 w-full" />
            <Skeleton className="h-24 w-full" />
            <Skeleton className="h-24 w-full" />
          </div>
          <Skeleton className="h-32 w-full" />
        </div>
      ) : period ? (
        <>
          <div className="mp-kpi-grid" role="region" aria-label={`${selectedLabel} snapshot`}>
            <div className="mp-kpi-card">
              <span className="mp-kpi-card__label">Status</span>
              <span className="mp-kpi-card__value">
                {period.run ? runStatusLabel(period.run.status) : "No run"}
              </span>
              <span className="mp-kpi-card__subtext">{selectedLabel}</span>
            </div>
            <div className="mp-kpi-card">
              <span className="mp-kpi-card__label">Employees</span>
              <span className="mp-kpi-card__value">{period.employees.length}</span>
              <span className="mp-kpi-card__subtext">On this month's roster</span>
            </div>
            <div className="mp-kpi-card">
              <span className="mp-kpi-card__label">Readiness</span>
              <span className="mp-kpi-card__value">{blockerCount === 0 ? "Clear" : `${blockerCount} missing`}</span>
              <span className="mp-kpi-card__subtext">{readiness.join(" · ")}</span>
            </div>
          </div>
          <section className="sa-payroll-card" aria-label={`${selectedLabel} payroll`}>
            <h2 className="sa-payroll-card__title">{selectedLabel}</h2>
            <p className="sa-payroll-card__lede">
              {period.run
                ? "Continue monthly inputs or open review."
                : "Start a draft for this month."}
            </p>
            <div className="sa-payroll-card__actions">
              {period.run ? (
                <>
                  <Link href={`/app/payroll/${year}/${month}`} className="sa-compose__submit">
                    Monthly inputs
                  </Link>
                  <Link href={`/app/payroll/${year}/${month}/review`} className="sa-compose__secondary">
                    Review
                  </Link>
                </>
              ) : (
                <Button type="button" onClick={() => void onStart()} loading={busy} loadingLabel="Starting…">
                  Start payroll
                </Button>
              )}
            </div>
          </section>
        </>
      ) : null}
    </main>
  );
}
