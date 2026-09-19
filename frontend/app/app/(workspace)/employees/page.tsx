"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import {
  EmployeeStatus,
  getEntitlements,
  listEmployees,
  type EmployeeListState,
  type Entitlements,
} from "@/lib/api";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { ListPager, usePager } from "@/components/ui/ListPager";
import { ListToolbar } from "@/components/ui/ListToolbar";
import { Skeleton } from "@/components/ui/Skeleton";
import { BulkUploadDialog } from "@/components/BulkUploadDialog";
import { BulkUploadSalaryDialog } from "@/components/BulkUploadSalaryDialog";
import { pageSlice } from "@/lib/paging";

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
  const [entitlements, setEntitlements] = useState<Entitlements | null>(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [query, setQuery] = useState("");
  const [status, setStatus] = useState("all");
  const [bulkUploadOpen, setBulkUploadOpen] = useState(false);
  const [bulkUploadSalaryOpen, setBulkUploadSalaryOpen] = useState(false);

  const loadData = async () => {
    try {
      const [list, loadedEntitlements] = await Promise.all([
        listEmployees(),
        getEntitlements().catch(() => null),
      ]);
      setState(list);
      setEntitlements(loadedEntitlements);
      setError("");
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : "Could not load employees.");
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    let cancelled = false;
    async function load() {
      try {
        const [list, loadedEntitlements] = await Promise.all([
          listEmployees(),
          getEntitlements().catch(() => null),
        ]);
        if (!cancelled) {
          setState(list);
          setEntitlements(loadedEntitlements);
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

  const activeCount = entitlements?.currentUsage ?? state?.activeCount ?? 0;
  const employeeLimit = entitlements?.maximumAllowed ?? state?.employeeLimit ?? 0;
  const seatsRemaining = entitlements?.remaining ?? state?.remaining;
  const draftCount = (state?.employees ?? []).filter(
    (employee) => employee.status === EmployeeStatus.Draft,
  ).length;

  return (
    <main className="sa-shell">
      <header className="sa-head sa-head--with-back">
        <h1>Employees</h1>
        <div style={{ display: "flex", gap: "var(--space-sm)", alignItems: "center" }}>
          <Button variant="ghost" onClick={() => setBulkUploadOpen(true)}>Bulk Upload</Button>
          <Button variant="ghost" onClick={() => setBulkUploadSalaryOpen(true)}>Upload Salaries</Button>
          <Link href="/app/employees/new" className="sa-compose__submit" style={{ margin: 0 }}>
            Add employee
          </Link>
        </div>
        <p>
          {state
            ? `${activeCount} / ${employeeLimit} active seats.`
            : "People on this company payroll."}
        </p>
      </header>

      <Alert>{error || null}</Alert>

      {loading && !state ? (
        <div className="space-y-4 py-4" role="status">
          <div className="mp-kpi-grid">
            <Skeleton className="h-24 w-full" />
            <Skeleton className="h-24 w-full" />
            <Skeleton className="h-24 w-full" />
          </div>
          <ol className="sa-index" aria-busy="true" aria-label="Loading employees">
            <li className="sa-index__row is-skeleton">
              <span className="sa-index__skeleton-bar" />
              <span className="sa-index__skeleton-bar" />
            </li>
            <li className="sa-index__row is-skeleton">
              <span className="sa-index__skeleton-bar" />
            </li>
          </ol>
        </div>
      ) : (
        <>
          {state ? (
            <div className="mp-kpi-grid" role="region" aria-label="Employees snapshot">
              <div className="mp-kpi-card">
                <span className="mp-kpi-card__label">Headcount</span>
                <span className="mp-kpi-card__value">{activeCount}</span>
                <span className="mp-kpi-card__subtext">
                  of {employeeLimit} {employeeLimit === 1 ? "seat" : "seats"}
                </span>
              </div>
              <div className="mp-kpi-card">
                <span className="mp-kpi-card__label">Seats</span>
                <span className="mp-kpi-card__value">
                  {seatsRemaining ?? "—"}
                </span>
                <span className="mp-kpi-card__subtext">
                  {seatsRemaining === 1 ? "seat remaining" : "seats remaining"}
                </span>
              </div>
              <div className="mp-kpi-card">
                <span className="mp-kpi-card__label">Drafts</span>
                <span className="mp-kpi-card__value">{draftCount}</span>
                <span className="mp-kpi-card__subtext">
                  {draftCount === 0
                    ? "None pending"
                    : draftCount === 1
                      ? "Not yet active"
                      : "Not yet activated"}
                </span>
              </div>
            </div>
          ) : null}

          {!state || state.employees.length === 0 ? (
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
                          <span className="sa-index__email">
                            {employee.designation || "—"}
                          </span>
                          <span className="sa-index__meta">
                            <span className="sa-chip">{statusLabel(employee.status)}</span>
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
        </>
      )}

      <BulkUploadDialog 
        open={bulkUploadOpen} 
        onOpenChange={setBulkUploadOpen} 
        onSuccess={loadData} 
      />
      <BulkUploadSalaryDialog 
        open={bulkUploadSalaryOpen} 
        onOpenChange={setBulkUploadSalaryOpen} 
        onSuccess={loadData}
      />
    </main>
  );
}
