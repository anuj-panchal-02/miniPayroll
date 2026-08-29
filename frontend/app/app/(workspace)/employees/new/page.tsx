"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { EmployeeForm } from "@/components/EmployeeForm";
import { createEmployee } from "@/lib/api";

export default function NewEmployeePage() {
  const router = useRouter();

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
        <h1>Add employee</h1>
        <p>Personal, employment, and bank details for this company.</p>
      </header>
      <EmployeeForm
        submitLabel="Save employee"
        onSave={async (input) => {
          const saved = await createEmployee(input);
          if (input.saveAsDraft) {
            router.replace(`/app/employees/${saved.id}`);
            return;
          }
          router.push("/app/employees");
        }}
      />
    </main>
  );
}
