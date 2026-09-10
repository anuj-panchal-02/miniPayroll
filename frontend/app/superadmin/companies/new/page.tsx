"use client";

import { FormEvent, useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { SuperadminShell } from "@/components/SuperadminShell";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Field } from "@/components/ui/Field";
import { FieldGroup } from "@/components/ui/FieldGroup";
import { createCompany, getPlatformLimits, getToken } from "@/lib/api";
import {
  DEFAULT_EMPLOYEE_LIMIT,
  DEFAULT_PLAN_NAME,
  FALLBACK_PLATFORM_LIMITS,
} from "@/lib/platform";
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
  const [employeeLimit, setEmployeeLimit] = useState(String(DEFAULT_EMPLOYEE_LIMIT));
  const [limits, setLimits] = useState(FALLBACK_PLATFORM_LIMITS);
  const [touched, setTouched] = useState<CreateTouched>({
    name: false,
    contactEmail: false,
    employeeLimit: false,
  });
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const nameFieldError = companyNameError(name);
  const emailFieldError = emailError(contactEmail, CONTACT_EMAIL_MESSAGES);
  const limitFieldError = employeeLimitError(employeeLimit, {
    min: limits.minEmployeeLimit,
    max: limits.hardEmployeeCap,
  });
  const shownNameError = touched.name ? nameFieldError : null;
  const shownEmailError = touched.contactEmail ? emailFieldError : null;
  const shownLimitError = touched.employeeLimit ? limitFieldError : null;

  useEffect(() => {
    if (!getToken()) {
      router.replace("/login");
      return;
    }

    let cancelled = false;
    getPlatformLimits()
      .then((loaded) => {
        if (cancelled) {
          return;
        }
        setLimits(loaded);
        setEmployeeLimit((current) =>
          current === String(DEFAULT_EMPLOYEE_LIMIT)
            ? String(loaded.defaultEmployeeLimit)
            : current,
        );
      })
      .catch(() => {
        /* keep fallback constants if the API is unavailable */
      });

    return () => {
      cancelled = true;
    };
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

        <form className="sa-compose" noValidate autoComplete="off" onSubmit={onCreate}>
          <FieldGroup title="Company" className="sa-compose__span">
            <Field id="company-name" label="Company name" error={shownNameError} required>
              <input
                ref={nameRef}
                className="mp-input"
                name="name"
                value={name}
                onChange={(e) => setName(e.target.value)}
                onBlur={() => markTouched("name")}
                placeholder="ABC Traders"
                disabled={busy}
              />
            </Field>
            <Field id="company-email" label="Contact email" error={shownEmailError} required>
              <input
                ref={emailRef}
                className="mp-input"
                name="contactEmail"
                type="email"
                value={contactEmail}
                onChange={(e) => setContactEmail(e.target.value)}
                onBlur={() => markTouched("contactEmail")}
                placeholder="owner@company.example"
                disabled={busy}
              />
            </Field>
          </FieldGroup>
          <FieldGroup
            title="Plan"
            hint="The Company Admin cannot change this cap."
            className="sa-compose__span"
          >
            <Field id="company-plan" label="Plan">
              <input
                className="mp-input"
                name="plan"
                value={DEFAULT_PLAN_NAME}
                disabled
                readOnly
              />
            </Field>
            <Field
              id="company-limit"
              label="Employee limit"
              hint={`Up to ${limits.hardEmployeeCap} employees. Superadmin can raise a company's limit later within this cap.`}
              error={shownLimitError}
              required
            >
              <input
                ref={limitRef}
                className="mp-input"
                name="employeeLimit"
                type="number"
                min={limits.minEmployeeLimit}
                max={limits.hardEmployeeCap}
                value={employeeLimit}
                onChange={(e) => setEmployeeLimit(e.target.value)}
                onBlur={() => markTouched("employeeLimit")}
                disabled={busy}
              />
            </Field>
          </FieldGroup>
          <Button type="submit" loading={busy} loadingLabel="Creating">
            Create company
          </Button>
        </form>

        <Alert>{error}</Alert>
      </main>
    </SuperadminShell>
  );
}
