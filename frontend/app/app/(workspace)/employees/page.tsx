"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { EmployeeStatus, listEmployees, type EmployeeListState } from "@/lib/api";
import { Alert } from "@/components/ui/Alert";
import { ListPager, usePager } from "@/components/ui/ListPager";
import { ListToolbar } from "@/components/ui/ListToolbar";
import { pageSlice } from "@/lib/paging";

export default function EmployeesPage() {
  const [state, setState] = useState<EmployeeListState | null>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [query, setQuery] = useState("");
  const [status, setStatus] = useState("all");

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

  const filtered = useMemo(() => {
    const employees = state?.employees ?? [];
    const needle = query.trim().toLowerCase();
    return employees.filter((employee) => {
      const matchesStatus = status === "all" || String(employee.status) === status;
      const matchesQuery =
        !needle ||
        employee.fullName.toLowerCase().includes(needle) ||
        employee.employeeCode.toLowerCase().includes(needle) ||
        employee.designation.toLowerCase().includes(needle);
      return matchesStatus && matchesQuery;
    });
  }, [state, query, status]);

  const pager = usePager(filtered.length, `${query}|${status}`);
  const visible = pageSlice(filtered, pager.page, pager.pageSize);

  const filtering = Boolean(query.trim()) || status !== "all";
  const resultText = `${filtered.length} ${filtered.length === 1 ? "employee" : "employees"}`;

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
      <Alert>{error || null}</Alert>
      {loading ? (
        <p className="sa-empty" role="status">
          Loading employees…
        </p>
      ) : !state || state.employees.length === 0 ? (
        <p className="sa-empty">No employees yet. Add one to start payroll later.</p>
      ) : (
        <>
          <ListToolbar
            searchId="employee-search"
            searchLabel="Search employees"
            searchValue={query}
            searchPlaceholder="Name, ID, or role"
            onSearchChange={setQuery}
            filterId="employee-status"
            filterLabel="Status"
            filterValue={status}
            filterOptions={[
              { value: "all", label: "All statuses" },
              { value: String(EmployeeStatus.Active), label: "Active" },
              { value: String(EmployeeStatus.Inactive), label: "Inactive" },
              { value: String(EmployeeStatus.Draft), label: "Draft" },
            ]}
            onFilterChange={setStatus}
            resultText={resultText}
            showReset={filtering}
            onReset={() => {
              setQuery("");
              setStatus("all");
            }}
          />
          {filtered.length === 0 ? (
            <p className="sa-empty">No employees match this search.</p>
          ) : (
            <>
            <ol className="sa-index">
              <li className="sa-index__legend" aria-hidden="true">
                <span>Employee</span>
                <span>Role</span>
                <span>Status</span>
                <span>Account</span>
              </li>
              {visible.map((employee) => (
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
            <ListPager
              id="employees"
              page={pager.page}
              pageSize={pager.pageSize}
              total={filtered.length}
              onPageChange={pager.setPage}
              onPageSizeChange={pager.setPageSize}
            />
            </>
          )}
        </>
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
