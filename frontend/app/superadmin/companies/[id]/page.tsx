"use client";

import { FormEvent, MouseEvent, useEffect, useId, useRef, useState } from "react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { SuperadminShell } from "@/components/SuperadminShell";
import {
  CompanyDetail,
  CreateAdminResponse,
  activateCompany,
  createCompanyAdmin,
  getCompany,
  getToken,
  setToken,
  updateCompanyLimit,
} from "@/lib/api";
import { emailError, employeeLimitError } from "@/lib/validation";

const ADMIN_EMAIL_MESSAGES = {
  empty: "Enter an admin email.",
  invalid: "Enter a valid admin email.",
};

export default function CompanyDetailsPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const id = params.id;
  const dialogRef = useRef<HTMLDialogElement>(null);
  const limitRef = useRef<HTMLInputElement>(null);
  const adminEmailRef = useRef<HTMLInputElement>(null);
  const titleId = useId();
  const copyId = useId();

  const [company, setCompany] = useState<CompanyDetail | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [adminEmail, setAdminEmail] = useState("");
  const [busy, setBusy] = useState(false);
  const [activating, setActivating] = useState(false);
  const [loading, setLoading] = useState(true);
  const [credentials, setCredentials] = useState<CreateAdminResponse | null>(null);
  const [copied, setCopied] = useState(false);
  const [employeeLimit, setEmployeeLimit] = useState("");
  const [savingLimit, setSavingLimit] = useState(false);
  const [limitTouched, setLimitTouched] = useState(false);
  const [adminEmailTouched, setAdminEmailTouched] = useState(false);

  const limitFieldError = employeeLimitError(employeeLimit);
  const adminEmailFieldError = emailError(adminEmail, ADMIN_EMAIL_MESSAGES);
  const shownLimitError = limitTouched ? limitFieldError : null;
  const shownAdminEmailError = adminEmailTouched ? adminEmailFieldError : null;

  useEffect(() => {
    if (!getToken()) {
      router.replace("/login");
      return;
    }

    let cancelled = false;
    async function load() {
      try {
        const detail = await getCompany(id);
        if (cancelled) {
          return;
        }
        setCompany(detail);
        setAdminEmail(detail.adminEmail ?? detail.contactEmail);
        setEmployeeLimit(String(detail.employeeLimit));
        setError(null);
      } catch (err) {
        if (cancelled) {
          return;
        }
        setError(err instanceof Error ? err.message : "Could not load company");
        if (String(err).toLowerCase().includes("unauthorized")) {
          setToken(null);
          router.push("/login");
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
  }, [id, router]);

  async function onCreateAdmin(event: FormEvent) {
    event.preventDefault();
    setAdminEmailTouched(true);
    if (adminEmailFieldError) {
      adminEmailRef.current?.focus();
      return;
    }
    setBusy(true);
    setError(null);
    setCopied(false);
    try {
      const created = await createCompanyAdmin(id, adminEmail);
      setCredentials(created);
      setCompany((current) =>
        current
          ? { ...current, hasAdmin: true, adminEmail: created.email }
          : current,
      );
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not create Company Admin");
    } finally {
      setBusy(false);
    }
  }

  async function copyPassword() {
    if (!credentials) {
      return;
    }
    try {
      await navigator.clipboard.writeText(credentials.temporaryPassword);
      setCopied(true);
    } catch {
      setError("Could not copy the password. Select it and copy manually.");
    }
  }

  function openActivateConfirm() {
    dialogRef.current?.showModal();
  }

  function closeActivateConfirm() {
    dialogRef.current?.close();
  }

  function handleBackdropClick(event: MouseEvent<HTMLDialogElement>) {
    if (event.target === event.currentTarget) {
      closeActivateConfirm();
    }
  }

  async function confirmActivate() {
    closeActivateConfirm();
    setActivating(true);
    setError(null);
    try {
      const result = await activateCompany(id);
      setCompany(result);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not activate company");
      if (String(err).toLowerCase().includes("unauthorized")) {
        setToken(null);
        router.push("/login");
      }
    } finally {
      setActivating(false);
    }
  }

  async function onSaveLimit(event: FormEvent) {
    event.preventDefault();
    setLimitTouched(true);
    if (limitFieldError) {
      limitRef.current?.focus();
      return;
    }
    setSavingLimit(true);
    setError(null);
    try {
      const result = await updateCompanyLimit(id, Number(employeeLimit));
      setCompany(result);
      setEmployeeLimit(String(result.employeeLimit));
    } catch (err) {
      setError(err instanceof Error ? err.message : "Could not update employee limit");
      if (String(err).toLowerCase().includes("unauthorized")) {
        setToken(null);
        router.push("/login");
      }
    } finally {
      setSavingLimit(false);
    }
  }

  const canActivate =
    company?.hasAdmin === true && company.status === "Pending";

  const intro = !company
    ? null
    : company.status === "Active"
      ? "Company is active. The Company Admin can sign in."
      : company.hasAdmin
        ? "Company Admin is set. Activate remains the next step."
        : "Create the one Company Admin for this company.";

  const heading = company?.name ?? "Company";
  const subhead = loading
    ? "Loading company details."
    : company
      ? intro
      : "This company was not found.";

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
          <h1>{heading}</h1>
          <p>{subhead}</p>
        </header>

        {company ? (
          <>
            <dl className="sa-facts">
              <div>
                <dt>Contact</dt>
                <dd>{company.contactEmail}</dd>
              </div>
              <div>
                <dt>Status</dt>
                <dd>
                  <span className="sa-chip" data-status={company.status}>
                    {company.status}
                  </span>
                </dd>
              </div>
              <div>
                <dt>Plan</dt>
                <dd>{company.planName}</dd>
              </div>
              <div>
                <dt>Admin</dt>
                <dd>{company.adminEmail ?? "Not created"}</dd>
              </div>
            </dl>

            <form className="sa-compose sa-compose--single" noValidate onSubmit={onSaveLimit}>
              <div className="sa-field">
                <label htmlFor="employee-limit">Employee limit</label>
                <input
                  ref={limitRef}
                  id="employee-limit"
                  name="employeeLimit"
                  type="number"
                  value={employeeLimit}
                  onChange={(e) => setEmployeeLimit(e.target.value)}
                  onBlur={() => setLimitTouched(true)}
                  disabled={savingLimit}
                  aria-required="true"
                  aria-invalid={shownLimitError ? true : undefined}
                  aria-describedby={shownLimitError ? "employee-limit-error" : undefined}
                />
                <p id="employee-limit-error" className="sa-field__error" role="alert">
                  {shownLimitError}
                </p>
              </div>
              <button
                type="submit"
                className="sa-compose__submit"
                disabled={savingLimit}
                aria-busy={savingLimit}
              >
                {savingLimit ? "Saving" : "Save"}
              </button>
            </form>

            {credentials ? (
              <section className="sa-secret" aria-live="polite">
                <h2>Temporary password</h2>
                <p>
                  Shown once. Give it to {credentials.email}. They must change it
                  at first login.
                </p>
                <p className="sa-secret__value">{credentials.temporaryPassword}</p>
                <button type="button" className="sa-copy" onClick={() => void copyPassword()}>
                  {copied ? "Copied" : "Copy"}
                </button>
              </section>
            ) : company.hasAdmin ? null : (
              <form className="sa-compose sa-compose--single" noValidate onSubmit={onCreateAdmin}>
                <div className="sa-field">
                  <label htmlFor="admin-email">Admin email</label>
                  <input
                    ref={adminEmailRef}
                    id="admin-email"
                    name="email"
                    type="email"
                    value={adminEmail}
                    onChange={(e) => setAdminEmail(e.target.value)}
                    onBlur={() => setAdminEmailTouched(true)}
                    disabled={busy}
                    autoComplete="email"
                    aria-required="true"
                    aria-invalid={shownAdminEmailError ? true : undefined}
                    aria-describedby={
                      shownAdminEmailError ? "admin-email-error" : undefined
                    }
                  />
                  <p id="admin-email-error" className="sa-field__error" role="alert">
                    {shownAdminEmailError}
                  </p>
                </div>
                <button
                  type="submit"
                  className="sa-compose__submit"
                  disabled={busy}
                  aria-busy={busy}
                >
                  {busy ? "Creating" : "Create admin"}
                </button>
              </form>
            )}

            {canActivate ? (
              <>
                <div className="sa-activate">
                  <button
                    type="button"
                    className="sa-compose__submit"
                    disabled={activating}
                    aria-busy={activating}
                    aria-haspopup="dialog"
                    onClick={openActivateConfirm}
                  >
                    {activating ? "Activating" : "Activate"}
                  </button>
                </div>

                <dialog
                  ref={dialogRef}
                  className="sa-dialog"
                  aria-labelledby={titleId}
                  aria-describedby={copyId}
                  onClick={handleBackdropClick}
                >
                  <form method="dialog" className="sa-dialog__body">
                    <h2 id={titleId} className="sa-dialog__title">
                      Activate company
                    </h2>
                    <p id={copyId} className="sa-dialog__copy">
                      This starts the billing period. The Company Admin can then sign in.
                    </p>
                    <div className="sa-dialog__actions">
                      <button
                        type="button"
                        className="sa-dialog__confirm"
                        onClick={() => void confirmActivate()}
                      >
                        Activate
                      </button>
                      <button type="submit" className="sa-dialog__cancel">
                        Cancel
                      </button>
                    </div>
                  </form>
                </dialog>
              </>
            ) : null}
          </>
        ) : null}

        <p className="sa-alert" role="alert">
          {error}
        </p>
      </main>
    </SuperadminShell>
  );
}
