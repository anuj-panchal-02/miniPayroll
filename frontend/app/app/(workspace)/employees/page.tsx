"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { EmployeeStatus, listEmployees, type EmployeeListState } from "@/lib/api";

export default function EmployeesPage() {
  const [state, setState] = useState<EmployeeListState | null>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;
    async function load() {
      try {
        const list = await listEmployees();
        if (!cancelled) {
          setState(list);
          setError("");
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

  return (
    <main className="sa-shell">
      <header className="sa-head sa-head--with-back">
        <h1>Employees</h1>
        <Link href="/app/employees/new" className="sa-compose__submit">
          Add employee
        </Link>
        <p>
          {state
            ? `${state.activeCount} / ${state.employeeLimit} active seats.`
            : "People on this company payroll."}
        </p>
      </header>
      {error ? (
        <p className="sa-alert" role="alert">
          {error}
        </p>
      ) : null}
      {loading ? (
        <p className="sa-empty" role="status">
          Loading employees…
        </p>
      ) : !state || state.employees.length === 0 ? (
        <p className="sa-empty">No employees yet. Add one to start payroll later.</p>
      ) : (
        <ol className="sa-index">
          <li className="sa-index__legend" aria-hidden="true">
            <span>Employee</span>
            <span>Role</span>
            <span>Status</span>
            <span>Account</span>
          </li>
          {state.employees.map((employee) => (
            <li key={employee.id}>
              <Link href={`/app/employees/${employee.id}`} className="sa-index__row">
                <span className="sa-index__name">
                  {employee.fullName} · {employee.employeeCode}
                </span>
                <span className="sa-index__email">{employee.designation}</span>
                <span className="sa-index__meta">
                  <span className="sa-chip">
                    {statusLabel(employee.status)}
                  </span>
                  <span className="sa-index__limit">{employee.maskedAccountNumber}</span>
                </span>
              </Link>
            </li>
          ))}
        </ol>
      )}
    </main>
  );
}

function statusLabel(status: EmployeeStatus): string {
  if (status === EmployeeStatus.Draft) {
    return "Draft";
  }
  if (status === EmployeeStatus.Inactive) {
    return "Inactive";
  }
  return "Active";
}
