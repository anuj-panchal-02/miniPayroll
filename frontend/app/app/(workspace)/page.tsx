"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { listEmployees, type EmployeeListState } from "@/lib/api";

export default function CompanyDashboardPage() {
  const [state, setState] = useState<EmployeeListState | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    let cancelled = false;
    async function load() {
      try {
        const list = await listEmployees();
        if (!cancelled) {
          setState(list);
        }
      } catch (reason) {
        if (!cancelled) {
          setError(reason instanceof Error ? reason.message : "Could not load employees.");
        }
      }
    }
    void load();
    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <main className="sa-shell">
      <header className="sa-head sa-head--with-back">
        <h1>Dashboard</h1>
        <Link href="/app/employees/new" className="sa-compose__submit">
          Add employee
        </Link>
        <p>
          {state
            ? `${state.activeCount} of ${state.employeeLimit} active employee seats in use.`
            : "Your company workspace after setup."}
        </p>
      </header>
      {error ? (
        <p className="sa-alert" role="alert">
          {error}
        </p>
      ) : null}
      <p>
        <Link href="/app/employees">View employees</Link>
      </p>
    </main>
  );
}
