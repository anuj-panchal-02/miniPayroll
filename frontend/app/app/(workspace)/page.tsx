"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import {
  getWorkspaceBilling,
  listEmployees,
  type CompanyBilling,
  type EmployeeListState,
} from "@/lib/api";
import { Alert } from "@/components/ui/Alert";
import { Skeleton } from "@/components/ui/Skeleton";
import {
  billingFormula,
  formatDueDate,
  formatRupees,
  periodLabel,
} from "@/lib/payroll";

function overdueCopy(billing: CompanyBilling): string | null {
  const pastGrace = billing.periods.some((period) => period.isPastGrace);
  const overdue = billing.periods.some((period) => period.isOverdue);
  if (pastGrace) {
    return "Payment grace has ended. You can still run payroll until your provider suspends the company.";
  }
  if (overdue) {
    return "Payment is overdue. You can keep running payroll during the grace period. Contact your service provider.";
  }
  return null;
}

export default function CompanyDashboardPage() {
  const [state, setState] = useState<EmployeeListState | null>(null);
  const [billing, setBilling] = useState<CompanyBilling | null>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    async function load() {
      try {
        const [list, loadedBilling] = await Promise.all([
          listEmployees(),
          getWorkspaceBilling().catch(() => null),
        ]);
        if (!cancelled) {
          setState(list);
          setBilling(loadedBilling);
        }
      } catch (reason) {
        if (!cancelled) {
          setError(reason instanceof Error ? reason.message : "Could not load employees.");
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
  }, []);

  const current =
    billing?.periods.find((period) => period.isEstimated) ?? billing?.periods.at(-1) ?? null;
  const banner = billing ? overdueCopy(billing) : null;

  const now = new Date();
  const currentMonthName = now.toLocaleString("default", { month: "long" });
  const currentYear = now.getFullYear();
  const calendarLabel = `${currentMonthName} ${currentYear}`;

  const activeCount = state?.activeCount ?? 0;
  const employeeLimit = state?.employeeLimit ?? 0;
  const seatsRemaining = Math.max(0, employeeLimit - activeCount);
  const draftCount = (state?.employees ?? []).filter((e) => e.status === 2).length;

  return (
    <main className="sa-shell">
      <header className="sa-head sa-head--with-back">
        <h1>Dashboard</h1>
        <Link href="/app/payroll/history" className="sa-compose__secondary">
          History
        </Link>
        <p>
          {state
            ? `${state.activeCount} of ${state.employeeLimit} active employee seats in use.`
            : "Your company workspace after setup."}
        </p>
      </header>

      <Alert>{error || null}</Alert>
      <Alert>{banner}</Alert>

      {loading && !state ? (
        <div className="space-y-4 py-4" role="status">
          <div className="mp-kpi-grid">
            <Skeleton className="h-24 w-full" />
            <Skeleton className="h-24 w-full" />
            <Skeleton className="h-24 w-full" />
          </div>
          <Skeleton className="h-32 w-full" />
        </div>
      ) : (
        <>
          <div className="mp-kpi-grid" role="region" aria-label="Workspace snapshot">
            <div className="mp-kpi-card">
              <span className="mp-kpi-card__label">Headcount</span>
              <span className="mp-kpi-card__value">{activeCount}</span>
              <span className="mp-kpi-card__subtext">
                {draftCount > 0
                  ? `${draftCount} ${draftCount === 1 ? "draft" : "drafts"} pending activation`
                  : `${seatsRemaining} ${seatsRemaining === 1 ? "seat" : "seats"} remaining`}
              </span>
            </div>
            <div className="mp-kpi-card">
              <span className="mp-kpi-card__label">Period</span>
              <span className="mp-kpi-card__value">{calendarLabel}</span>
              <span className="mp-kpi-card__subtext">Open calendar month</span>
            </div>
            <div className="mp-kpi-card">
              <span className="mp-kpi-card__label">Plan</span>
              <span className="mp-kpi-card__value">{billing?.planName ?? "Basic"}</span>
              <span className="mp-kpi-card__subtext">
                {current ? formatDueDate(current.dueDate) : "Next bill upcoming"}
              </span>
            </div>
          </div>

          <section className="sa-payroll-card" aria-label={`${calendarLabel} workspace`}>
            <h2 className="sa-payroll-card__title">{calendarLabel}</h2>
            <p className="sa-payroll-card__lede">
              Process this month's payroll, or add an employee to the roster.
            </p>
            <div className="sa-payroll-card__actions">
              <Link href="/app/payroll" className="sa-compose__submit">
                Process payroll
              </Link>
              <Link href="/app/employees/new" className="sa-compose__secondary">
                Add employee
              </Link>
            </div>
          </section>

          {current && billing ? (
            <section className="sa-payroll-card" aria-label="Subscription">
              <h2 className="sa-payroll-card__title">Subscription</h2>
              <p className="sa-payroll-card__lede">
                {current.isEstimated
                  ? "Estimated charges for this billing period."
                  : "Invoiced charges for this billing period."}
              </p>
              <dl className="sa-billing-facts">
                <div>
                  <dt>Plan</dt>
                  <dd>{billing.planName}</dd>
                </div>
                {state ? (
                  <div>
                    <dt>Seats</dt>
                    <dd>
                      {state.activeCount} of {state.employeeLimit}
                    </dd>
                  </div>
                ) : null}
                <div>
                  <dt>{periodLabel(current.year, current.month)}</dt>
                  <dd>
                    {billingFormula(
                      current.billableEmployees,
                      current.pricePerEmployee,
                      current.amountDue,
                    )}
                    {current.isEstimated ? " (estimated)" : ""}
                  </dd>
                </div>
                <div>
                  <dt>Due</dt>
                  <dd>{formatDueDate(current.dueDate)}</dd>
                </div>
                <div>
                  <dt>Paid</dt>
                  <dd>{formatRupees(current.paidAmount)}</dd>
                </div>
                <div>
                  <dt>Remaining</dt>
                  <dd>{formatRupees(current.remaining)}</dd>
                </div>
              </dl>
            </section>
          ) : null}
        </>
      )}
    </main>
  );
}
