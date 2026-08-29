"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { EmployeeForm } from "@/components/EmployeeForm";
import { SalaryStructurePanel } from "@/components/SalaryStructurePanel";
import { getEmployee, updateEmployee, type EmployeeDetail } from "@/lib/api";

export default function EditEmployeePage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const [employee, setEmployee] = useState<EmployeeDetail | null>(null);
  const [error, setError] = useState("");

  useEffect(() => {
    let cancelled = false;
    async function load() {
      try {
        const detail = await getEmployee(params.id);
        if (!cancelled) {
          setEmployee(detail);
        }
      } catch (reason) {
        if (!cancelled) {
          setError(reason instanceof Error ? reason.message : "Could not load employee.");
        }
      }
    }
    void load();
    return () => {
      cancelled = true;
    };
  }, [params.id]);

  return (
    <main className="sa-shell">
      <header className="sa-head sa-head--with-back">
        <Link href="/app/employees" className="sa-back" aria-label="Employees">
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
        <h1>Edit employee</h1>
        <p>Update details without changing payroll history.</p>
      </header>
      {error ? (
        <p className="sa-alert" role="alert">
          {error}
        </p>
      ) : null}
      {employee ? (
        <>
          <EmployeeForm
            employee={employee}
            submitLabel="Save changes"
            onSave={async (input) => {
              await updateEmployee(employee.id, input);
              if (input.saveAsDraft) {
                return;
              }
              router.push("/app/employees");
            }}
          />
          <SalaryStructurePanel employee={employee} />
        </>
      ) : error ? null : (
        <p className="sa-empty" role="status">
          Loading employee…
        </p>
      )}
    </main>
  );
}
