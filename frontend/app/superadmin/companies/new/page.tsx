"use client";

import { FormEvent, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { SuperadminShell } from "@/components/SuperadminShell";
import { createCompany, getToken } from "@/lib/api";
import { DEFAULT_PLAN_NAME } from "@/lib/platform";
import {
  companyNameError,
  emailError,
  employeeLimitError,
} from "@/lib/validation";

const CONTACT_EMAIL_MESSAGES = {
  empty: "Enter a contact email.",
  invalid: "Enter a valid contact email.",
};

type CreateTouched = {
  name: boolean;
  contactEmail: boolean;
  employeeLimit: boolean;
};

export default function CreateCompanyPage() {
  const router = useRouter();
  const nameRef = useRef<HTMLInputElement>(null);
  const emailRef = useRef<HTMLInputElement>(null);
  const limitRef = useRef<HTMLInputElement>(null);

  const [name, setName] = useState("");
  const [contactEmail, setContactEmail] = useState("");
  const [employeeLimit, setEmployeeLimit] = useState("");
  const [touched, setTouched] = useState<CreateTouched>({
    name: false,
    contactEmail: false,
    employeeLimit: false,
  });
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const nameFieldError = companyNameError(name);
  const emailFieldError = emailError(contactEmail, CONTACT_EMAIL_MESSAGES);
  const limitFieldError = employeeLimitError(employeeLimit);
  const shownNameError = touched.name ? nameFieldError : null;
  const shownEmailError = touched.contactEmail ? emailFieldError : null;
  const shownLimitError = touched.employeeLimit ? limitFieldError : null;

  useEffect(() => {
    if (!getToken()) {
      router.replace("/login");
    }
  }, [router]);

  function markTouched(field: keyof CreateTouched) {
    setTouched((current) => ({ ...current, [field]: true }));
  }

  async function onCreate(event: FormEvent) {
    event.preventDefault();
    setTouched({ name: true, contactEmail: true, employeeLimit: true });
    if (nameFieldError) {
      nameRef.current?.focus();
      return;
    }
    if (emailFieldError) {
      emailRef.current?.focus();
      return;
    }
    if (limitFieldError) {
      limitRef.current?.focus();
      return;
    }

    setBusy(true);
    setError(null);
    try {
      const created = await createCompany({
        name,
        contactEmail,
        employeeLimit: Number(employeeLimit),
      });
      router.push(`/superadmin/companies/${created.id}`);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not create company");
      setBusy(false);
    }
  }

  return (
    <SuperadminShell>
      <main className="sa-shell">
        <header className="sa-head sa-head--with-back">
          <Link href="/superadmin" className="sa-back" aria-label="Companies">
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
          <h1>Create company</h1>
          <p>
            Superadmin sets the plan and employee limit. The Company Admin completes
            company details later.
          </p>
        </header>

        <form className="sa-compose" noValidate onSubmit={onCreate}>
          <div className="sa-field">
            <label htmlFor="company-name">Company name</label>
            <input
              ref={nameRef}
              id="company-name"
              name="name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              onBlur={() => markTouched("name")}
              placeholder="ABC Traders"
              disabled={busy}
              autoComplete="organization"
              aria-required="true"
              aria-invalid={shownNameError ? true : undefined}
              aria-describedby={shownNameError ? "company-name-error" : undefined}
            />
            <p id="company-name-error" className="sa-field__error" role="alert">
              {shownNameError}
            </p>
          </div>
          <div className="sa-field">
            <label htmlFor="company-email">Contact email</label>
            <input
              ref={emailRef}
              id="company-email"
              name="contactEmail"
              type="email"
              value={contactEmail}
              onChange={(e) => setContactEmail(e.target.value)}
              onBlur={() => markTouched("contactEmail")}
              placeholder="owner@company.example"
              disabled={busy}
              autoComplete="email"
              aria-required="true"
              aria-invalid={shownEmailError ? true : undefined}
              aria-describedby={shownEmailError ? "company-email-error" : undefined}
            />
            <p id="company-email-error" className="sa-field__error" role="alert">
              {shownEmailError}
            </p>
          </div>
          <div className="sa-field">
            <label htmlFor="company-plan">Plan</label>
            <input
              id="company-plan"
              name="plan"
              value={DEFAULT_PLAN_NAME}
              disabled
              readOnly
            />
            <p className="sa-field__error" aria-hidden="true" />
          </div>
          <div className="sa-field">
            <label htmlFor="company-limit">Employee limit</label>
            <input
              ref={limitRef}
              id="company-limit"
              name="employeeLimit"
              type="number"
              value={employeeLimit}
              onChange={(e) => setEmployeeLimit(e.target.value)}
              onBlur={() => markTouched("employeeLimit")}
              disabled={busy}
              aria-required="true"
              aria-invalid={shownLimitError ? true : undefined}
              aria-describedby={shownLimitError ? "company-limit-error" : undefined}
            />
            <p id="company-limit-error" className="sa-field__error" role="alert">
              {shownLimitError}
            </p>
          </div>
          <button
            type="submit"
            className="sa-compose__submit"
            disabled={busy}
            aria-busy={busy}
          >
            {busy ? "Creating" : "Create company"}
          </button>
        </form>

        <p className="sa-alert" role="alert">
          {error}
        </p>
      </main>
    </SuperadminShell>
  );
}
