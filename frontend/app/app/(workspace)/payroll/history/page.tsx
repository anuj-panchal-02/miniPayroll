"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import {
  PayrollRunStatus,
  listPayrollRuns,
  type PayrollHistoryItem,
} from "@/lib/api";
import { Alert } from "@/components/ui/Alert";
import { ListPager, usePager } from "@/components/ui/ListPager";
import { formatRupees, periodLabel, runStatusLabel } from "@/lib/payroll";
import { pageSlice } from "@/lib/paging";

export default function PayrollHistoryPage() {
  const [runs, setRuns] = useState<PayrollHistoryItem[]>([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    listPayrollRuns()
      .then((items) => {
        if (!cancelled) {
          setRuns(items);
          setError("");
        }
      })
      .catch((reason) => {
        if (!cancelled) {
          setError(reason instanceof Error ? reason.message : "Could not load payroll history.");
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  const pager = usePager(runs.length);
  const visible = pageSlice(runs, pager.page, pager.pageSize);

  const closed = useMemo(
    () =>
      runs.filter(
        (run) =>
          run.status === PayrollRunStatus.Finalized || run.status === PayrollRunStatus.Reversed,
      ).length,
    [runs],
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
        <h1>Payroll history</h1>
        <p>
          {runs.length === 0
            ? "Closed and in-progress runs for this company."
            : `${closed} closed · ${runs.length} total.`}
        </p>
      </header>
      <Alert>{error || null}</Alert>
      {loading ? (
        <p className="sa-empty" role="status">
          Loading history…
        </p>
      ) : runs.length === 0 ? (
        <p className="sa-empty">No payroll runs yet.</p>
      ) : (
        <>
          <div className="sa-master-wrap">
            <table className="sa-master">
              <thead>
                <tr>
                  <th scope="col">Month</th>
                  <th scope="col">Status</th>
                  <th scope="col">Employees</th>
                  <th scope="col">Net</th>
                </tr>
              </thead>
              <tbody>
                {visible.map((run) => (
                  <tr key={run.id}>
                    <td>
                      <Link href={`/app/payroll/${run.year}/${run.month}/review`}>
                        {periodLabel(run.year, run.month)}
                      </Link>
                    </td>
                    <td>
                      <span className="sa-chip">{runStatusLabel(run.status)}</span>
                    </td>
                    <td>{run.employeeCount}</td>
                    <td>{formatRupees(run.netSalary)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <ListPager
            id="payroll-history"
            page={pager.page}
            pageSize={pager.pageSize}
            total={runs.length}
            onPageChange={pager.setPage}
            onPageSizeChange={pager.setPageSize}
          />
        </>
      )}
    </main>
  );
}
