"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { EmployeeStatus, listEmployees, type EmployeeListState } from "@/lib/api";
import { Alert } from "@/components/ui/Alert";
import { Badge } from "@/components/ui/Badge";
import { ListPager, usePager } from "@/components/ui/ListPager";
import { ListToolbar } from "@/components/ui/ListToolbar";
import { Skeleton } from "@/components/ui/Skeleton";
import { pageSlice } from "@/lib/paging";

function initials(name: string): string {
  const parts = name.trim().split(/\s+/);
  if (parts.length >= 2) {
    return (parts[0][0] + parts[1][0]).toUpperCase();
  }
  return name.slice(0, 2).toUpperCase() || "MP";
}

function statusBadgeTone(status: EmployeeStatus): "success" | "warning" | "neutral" {
  if (status === EmployeeStatus.Active) return "success";
  if (status === EmployeeStatus.Draft) return "warning";
  return "neutral";
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

  const activeCount = state?.activeCount ?? 0;
  const employeeLimit = state?.employeeLimit ?? 50;
  const seatPct = employeeLimit > 0 ? Math.min(100, Math.round((activeCount / employeeLimit) * 100)) : 0;

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

      {/* Seat Meter Header Card */}
      {state && !loading ? (
        <div className="mp-seat-meter mb-6">
          <div className="mp-seat-meter__header">
            <span className="mp-seat-meter__label">Active Headcount & Seats</span>
            <span className="mp-seat-meter__value">
              {activeCount} / {employeeLimit} active ({seatPct}%)
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
        </div>
      ) : null}

      {loading ? (
        <div className="space-y-4 py-8">
          <p className="sa-empty" role="status">
            Loading employees…
          </p>
          <div className="space-y-3">
            <Skeleton className="h-12 w-full" />
            <Skeleton className="h-14 w-full" />
            <Skeleton className="h-14 w-full" />
          </div>
        </div>
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
                    <Link href={`/app/employees/${employee.id}`} className="sa-index__row items-center">
                      <div className="flex items-center gap-3 min-w-0">
                        <div
                          className="w-8 h-8 rounded-full bg-paper-3 text-ink font-semibold flex items-center justify-center text-xs shrink-0 select-none"
                          aria-hidden="true"
                        >
                          {initials(employee.fullName)}
                        </div>
                        <span className="sa-index__name truncate">
                          {employee.fullName} · {employee.employeeCode}
                        </span>
                      </div>
                      <span className="sa-index__email truncate">
                        {employee.designation || "—"}
                      </span>
                      <span className="sa-index__meta">
                        <Badge tone={statusBadgeTone(employee.status)}>
                          {statusLabel(employee.status)}
                        </Badge>
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
