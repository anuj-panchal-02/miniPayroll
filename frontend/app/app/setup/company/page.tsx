"use client";

import { FormEvent, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { SetupWizardShell } from "@/components/SetupWizardShell";
import {
  getCompanySetup,
  updateCompanyDetails,
  uploadCompanyLogo,
} from "@/lib/api";
import {
  type CompanySetupErrors,
  type CompanySetupField,
  type CompanySetupFields,
  companySetupErrors,
  logoFileError,
} from "@/lib/validation";

const EMPTY_DETAILS: CompanySetupFields = {
  name: "",
  contactEmail: "",
  contactPhone: "",
  addressLine1: "",
  addressLine2: "",
  city: "",
  state: "",
  postalCode: "",
};

const FIELD_ORDER: CompanySetupField[] = [
  "name",
  "contactEmail",
  "contactPhone",
  "addressLine1",
  "addressLine2",
  "city",
  "state",
  "postalCode",
];

const FIELD_CONFIG: Array<{
  name: CompanySetupField;
  label: string;
  type?: string;
  autoComplete: string;
}> = [
  { name: "name", label: "Company name", autoComplete: "organization" },
  { name: "contactEmail", label: "Contact email", type: "email", autoComplete: "email" },
  { name: "contactPhone", label: "Phone", type: "tel", autoComplete: "tel" },
  { name: "addressLine1", label: "Address line 1", autoComplete: "address-line1" },
  { name: "addressLine2", label: "Address line 2 (optional)", autoComplete: "address-line2" },
  { name: "city", label: "City", autoComplete: "address-level2" },
  { name: "state", label: "State", autoComplete: "address-level1" },
  { name: "postalCode", label: "Postal code", autoComplete: "postal-code" },
];

export default function CompanySetupPage() {
  const router = useRouter();
  const [values, setValues] = useState(EMPTY_DETAILS);
  const [errors, setErrors] = useState<CompanySetupErrors>({});
  const [logoError, setLogoError] = useState("");
  const [logoFile, setLogoFile] = useState<File | null>(null);
  const [logoUrl, setLogoUrl] = useState<string | null>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [loadError, setLoadError] = useState("");
  const [submitError, setSubmitError] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const fieldRefs = useRef<Partial<Record<CompanySetupField, HTMLInputElement | null>>>({});
  const logoRef = useRef<HTMLInputElement>(null);
  const mountedRef = useRef(false);
  const submissionRef = useRef(0);
  const previewUrlRef = useRef<string | null>(null);

  useEffect(() => {
    mountedRef.current = true;
    let cancelled = false;
    getCompanySetup()
      .then((setup) => {
        if (cancelled) return;
        setValues({
          name: setup.name,
          contactEmail: setup.contactEmail,
          contactPhone: setup.contactPhone ?? "",
          addressLine1: setup.addressLine1 ?? "",
          addressLine2: setup.addressLine2 ?? "",
          city: setup.city ?? "",
          state: setup.state ?? "",
          postalCode: setup.postalCode ?? "",
        });
        setLogoUrl(setup.logoUrl);
      })
      .catch((reason) => {
        if (!cancelled) {
          setLoadError(reason instanceof Error ? reason.message : "Unable to load company details.");
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
      mountedRef.current = false;
      submissionRef.current += 1;
      if (previewUrlRef.current) {
        URL.revokeObjectURL?.(previewUrlRef.current);
        previewUrlRef.current = null;
      }
    };
  }, []);

  function replacePreviewUrl(nextUrl: string | null) {
    if (previewUrlRef.current) {
      URL.revokeObjectURL?.(previewUrlRef.current);
    }
    previewUrlRef.current = nextUrl;
    setPreviewUrl(nextUrl);
  }

  function selectLogo(file: File | null) {
    replacePreviewUrl(null);
    setLogoFile(null);
    setLogoError("");
    if (!file) return;

    const error = logoFileError(file);
    if (error) {
      setLogoError(error);
      return;
    }

    setLogoFile(file);
    if (typeof URL.createObjectURL === "function") {
      replacePreviewUrl(URL.createObjectURL(file));
    }
  }

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const nextErrors = companySetupErrors(values);
    const nextLogoError = logoFile || logoUrl ? "" : "Choose a company logo.";
    setErrors(nextErrors);
    setLogoError((current) => current || nextLogoError);
    setSubmitError("");

    const firstInvalid = FIELD_ORDER.find((field) => nextErrors[field]);
    if (firstInvalid) {
      fieldRefs.current[firstInvalid]?.focus();
      return;
    }
    if (logoError || nextLogoError) {
      logoRef.current?.focus();
      return;
    }

    setSaving(true);
    const submission = ++submissionRef.current;
    const isCurrentSubmission = () =>
      mountedRef.current && submissionRef.current === submission;
    try {
      if (logoFile) {
        const uploaded = await uploadCompanyLogo(logoFile);
        if (!isCurrentSubmission()) return;
        setLogoUrl(uploaded.logoUrl);
      }
      await updateCompanyDetails({
        name: values.name.trim(),
        contactEmail: values.contactEmail.trim(),
        contactPhone: values.contactPhone.trim(),
        addressLine1: values.addressLine1.trim(),
        addressLine2: values.addressLine2.trim() || null,
        city: values.city.trim(),
        state: values.state.trim(),
        postalCode: values.postalCode.trim(),
      });
      if (!isCurrentSubmission()) return;
      router.push("/app/setup/payroll");
    } catch (reason) {
      if (isCurrentSubmission()) {
        setSubmitError(reason instanceof Error ? reason.message : "Unable to save company details.");
      }
    } finally {
      if (isCurrentSubmission()) {
        setSaving(false);
      }
    }
  }

  return (
    <SetupWizardShell
      currentStep={1}
      title="Company details"
      description="Add the legal and contact details used across payroll records."
    >
      {loading ? <p className="setup-loading" role="status">Loading company details…</p> : null}
      {loadError ? <p className="setup-alert" role="alert">{loadError}</p> : null}
      {!loading && !loadError ? (
        <form className="setup-form" noValidate onSubmit={submit}>
          <div className="setup-form__grid">
            {FIELD_CONFIG.map(({ name, label, type = "text", autoComplete }) => (
              <div
                className={`setup-field${name.startsWith("address") ? " setup-field--wide" : ""}`}
                key={name}
              >
                <label htmlFor={`company-${name}`}>{label}</label>
                <input
                  ref={(element) => {
                    fieldRefs.current[name] = element;
                  }}
                  id={`company-${name}`}
                  name={name}
                  type={type}
                  autoComplete={autoComplete}
                  value={values[name]}
                  aria-invalid={Boolean(errors[name])}
                  aria-describedby={errors[name] ? `company-${name}-error` : undefined}
                  onChange={(event) => {
                    setValues((current) => ({ ...current, [name]: event.target.value }));
                    setErrors((current) => {
                      const next = { ...current };
                      delete next[name];
                      return next;
                    });
                  }}
                />
                {errors[name] ? (
                  <p id={`company-${name}-error`} className="setup-field__error">
                    {errors[name]}
                  </p>
                ) : null}
              </div>
            ))}
          </div>

          <div className="setup-logo">
            <div>
              <label htmlFor="company-logo">Company logo</label>
              <p>PNG, JPEG, or WebP. Maximum 2 MB.</p>
              <input
                ref={logoRef}
                id="company-logo"
                type="file"
                accept="image/png,image/jpeg,image/webp"
                aria-invalid={Boolean(logoError)}
                aria-describedby={logoError ? "company-logo-error" : undefined}
                onChange={(event) => selectLogo(event.target.files?.[0] ?? null)}
              />
              {logoError ? (
                <p id="company-logo-error" className="setup-field__error">
                  {logoError}
                </p>
              ) : null}
            </div>
            {previewUrl || logoUrl ? (
              // eslint-disable-next-line @next/next/no-img-element
              <img className="setup-logo__preview" src={previewUrl ?? logoUrl ?? ""} alt="Company logo preview" />
            ) : (
              <div className="setup-logo__placeholder" aria-hidden="true">Logo</div>
            )}
          </div>

          {FIELD_ORDER.some((field) => Boolean(errors[field])) || logoError ? (
            <p className="setup-alert" role="alert">
              Review the highlighted fields. The first issue is{" "}
              {errors[FIELD_ORDER.find((field) => errors[field]) ?? "name"] ?? logoError}
            </p>
          ) : null}
          {submitError ? <p className="setup-alert" role="alert">{submitError}</p> : null}

          <div className="setup-actions">
            <button className="setup-button" type="submit" disabled={saving} aria-busy={saving}>
              {saving ? "Saving…" : "Save and continue"}
            </button>
          </div>
        </form>
      ) : null}
    </SetupWizardShell>
  );
}
