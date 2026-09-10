"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  getPayrollPeriod,
  createPayrollRun,
  type PayrollPeriodDetail,
} from "@/lib/api";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Field } from "@/components/ui/Field";
import { FieldGroup } from "@/components/ui/FieldGroup";
import { MONTH_LABELS, periodLabel, runStatusLabel } from "@/lib/payroll";

function currentPeriod() {
  const now = new Date();
  return { year: now.getFullYear(), month: now.getMonth() + 1 };
}

export default function PayrollPage() {
  const router = useRouter();
  const initial = currentPeriod();
  const [year, setYear] = useState(initial.year);
  const [month, setMonth] = useState(initial.month);
  const [period, setPeriod] = useState<PayrollPeriodDetail | null>(null);
  const [error, setError] = useState("");
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

  async function onStart() {
    setBusy(true);
    try {
      await createPayrollRun(year, month);
      router.push(`/app/payroll/${year}/${month}`);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not start payroll.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <main className="sa-shell">
      <header className="sa-head">
        <h1>Payroll</h1>
        <p>Prepare monthly inputs, calculate, and review a draft run.</p>
      </header>
      <FieldGroup title="Payroll month">
        <Field id="payroll-month" label="Month">
          <select
            className="mp-select"
            value={month}
            onChange={(event) => setMonth(Number(event.target.value))}
          >
            {MONTH_LABELS.map((label, index) => (
              <option key={label} value={index + 1}>
                {label}
              </option>
            ))}
          </select>
        </Field>
        <Field id="payroll-year" label="Year">
          <select
            className="mp-select"
            value={year}
            onChange={(event) => setYear(Number(event.target.value))}
          >
            {years.map((item) => (
              <option key={item} value={item}>
                {item}
              </option>
            ))}
          </select>
        </Field>
      </FieldGroup>
      <Alert>{error || null}</Alert>
      {loading ? (
        <p className="sa-empty" role="status">
          Loading payroll…
        </p>
      ) : period ? (
        <section className="sa-payroll-card" aria-label={`${periodLabel(year, month)} payroll`}>
          <p className="sa-payroll-card__status">
            {period.run
              ? `${runStatusLabel(period.run.status)} · ${period.employees.length} employees`
              : `No run yet · ${period.employees.length} employees`}
          </p>
          <p>
            {missingStructure} missing salary structure
            {period.run ? ` · ${missingAttendance} missing attendance` : ""}.
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
      ) : null}
    </main>
  );
}
