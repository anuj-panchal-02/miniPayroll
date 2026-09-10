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
import { Badge } from "@/components/ui/Badge";
import { FieldGroup } from "@/components/ui/FieldGroup";
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

  // Seat metrics
  const activeCount = state?.activeCount ?? 0;
  const employeeLimit = state?.employeeLimit ?? 50;
  const seatPct = employeeLimit > 0 ? Math.min(100, Math.round((activeCount / employeeLimit) * 100)) : 0;
  const draftCount = (state?.employees ?? []).filter((e) => e.status === 2).length;

  return (
    <main className="sa-shell">
      <header className="sa-head sa-head--with-back">
        <h1>Dashboard</h1>
        <div className="flex items-center gap-2">
          <Link href="/app/employees/new" className="sa-compose__submit">
            + Add employee
          </Link>
          <Link href="/app/payroll" className="sa-compose__secondary">
            Process payroll
          </Link>
        </div>
        <p>
          {state
            ? `${state.activeCount} of ${state.employeeLimit} active employee seats in use.`
            : "Your company workspace after setup."}
        </p>
      </header>

      <Alert>{error || null}</Alert>
      <Alert>{banner}</Alert>

      {loading ? (
        <div className="space-y-4 py-4">
          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <Skeleton className="h-24 w-full" />
            <Skeleton className="h-24 w-full" />
            <Skeleton className="h-24 w-full" />
          </div>
          <Skeleton className="h-32 w-full" />
        </div>
      ) : (
        <>
          {/* Actionable KPIs */}
          <div className="mp-kpi-grid">
            <div className="mp-kpi-card">
              <span className="mp-kpi-card__label">Active Headcount</span>
              <span className="mp-kpi-card__value">{activeCount}</span>
              <span className="mp-kpi-card__subtext">
                {draftCount > 0 ? `${draftCount} drafts pending activation` : "All employees active"}
              </span>
            </div>

            <div className="mp-kpi-card">
              <span className="mp-kpi-card__label">Current Pay Period</span>
              <span className="mp-kpi-card__value">
                {currentMonthName} {currentYear}
              </span>
              <span className="mp-kpi-card__subtext">
                <Link href="/app/payroll" className="text-accent underline font-medium">
                  Open pay cycle →
                </Link>
              </span>
            </div>

            <div className="mp-kpi-card">
              <span className="mp-kpi-card__label">Subscription Plan</span>
              <span className="mp-kpi-card__value">{billing?.planName ?? "Basic"}</span>
              <span className="mp-kpi-card__subtext">
                {current ? formatDueDate(current.dueDate) : "Next bill upcoming"}
              </span>
            </div>
          </div>

          {/* Seat Utilization Bar */}
          {state ? (
            <div className="mp-seat-meter mb-6">
              <div className="mp-seat-meter__header">
                <span className="mp-seat-meter__label">Seat Utilization</span>
                <span className="mp-seat-meter__value">
                  {activeCount} of {employeeLimit} seats ({seatPct}%)
                </span>
              </div>
              <div className="mp-seat-meter__track">
                <div
                  className={`mp-seat-meter__fill ${
                    seatPct >= 100
                      ? "mp-seat-meter__fill--full"
                      : seatPct >= 80
                        ? "mp-seat-meter__fill--warning"
                        : ""
                  }`}
                  style={{ width: `${seatPct}%` }}
                />
              </div>
              <div className="flex justify-between text-xs text-muted-foreground mt-1">
                <span>{employeeLimit - activeCount} seats remaining</span>
                <span>Max platform limit: 50</span>
              </div>
            </div>
          ) : null}

          {/* Subscription Facts Group (Preserved for full compatibility) */}
          {current && billing ? (
            <FieldGroup title="Subscription">
              <dl className="sa-facts">
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
                  <dt>Paid / remaining</dt>
                  <dd>
                    {formatRupees(current.paidAmount)} / {formatRupees(current.remaining)}
                  </dd>
                </div>
              </dl>
            </FieldGroup>
          ) : null}

          {/* Fast Navigation Grid */}
          <div className="mt-8 grid grid-cols-1 sm:grid-cols-2 gap-4">
            <Link href="/app/employees" className="mp-nav-card">
              <div>
                <h3 className="mp-nav-card__title">Employee Directory</h3>
                <p className="mp-nav-card__desc">
                  Manage profiles, designations, and salary structures.
                </p>
              </div>
              <span className="mp-nav-card__arrow" aria-hidden="true">→</span>
            </Link>

            <Link href="/app/payroll/history" className="mp-nav-card">
              <div>
                <h3 className="mp-nav-card__title">Payroll History & Payslips</h3>
                <p className="mp-nav-card__desc">
                  Download closed runs and employee payslip PDFs.
                </p>
              </div>
              <span className="mp-nav-card__arrow" aria-hidden="true">→</span>
            </Link>
          </div>
        </>
      )}
    </main>
  );
}
