"use client";

import { FormEvent, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { SetupWizardShell } from "@/components/SetupWizardShell";
import { ToastOutlet, useToast } from "@/components/Toast";
import { Button } from "@/components/ui/Button";
import { Field } from "@/components/ui/Field";
import { FieldGroup } from "@/components/ui/FieldGroup";
import { FileField } from "@/components/ui/FileField";
import { LocationFields } from "@/components/LocationFields";
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

export default function CompanySetupPage() {
  const router = useRouter();
  const [values, setValues] = useState(EMPTY_DETAILS);
  const [errors, setErrors] = useState<CompanySetupErrors>({});
  const [logoError, setLogoError] = useState("");
  const [logoFile, setLogoFile] = useState<File | null>(null);
  const [logoUrl, setLogoUrl] = useState<string | null>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [loadError, setLoadError] = useState("");
  const toast = useToast();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const fieldRefs = useRef<Partial<Record<CompanySetupField, HTMLElement | null>>>({});
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
    toast.dismiss();

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
        toast.showError(reason instanceof Error ? reason.message : "Unable to save company details.");
      }
    } finally {
      if (isCurrentSubmission()) {
        setSaving(false);
      }
    }
  }

  function updateField(name: CompanySetupField, value: string) {
    setValues((current) => ({ ...current, [name]: value }));
    setErrors((current) => {
      const next = { ...current };
      delete next[name];
      return next;
    });
  }

  function blurField(name: CompanySetupField) {
    const message = companySetupErrors(values)[name];
    if (!message) return;
    setErrors((current) => ({ ...current, [name]: message }));
  }

  function renderField(
    name: CompanySetupField,
    label: string,
    extra?: { type?: string; className?: string },
  ) {
    return (
      <Field
        id={`company-${name}`}
        label={label}
        error={errors[name]}
        className={extra?.className}
      >
        <input
          ref={(element) => {
            fieldRefs.current[name] = element;
          }}
          className="mp-input"
          name={name}
          type={extra?.type ?? "text"}
          value={values[name]}
          onChange={(event) => updateField(name, event.target.value)}
          onBlur={() => blurField(name)}
        />
      </Field>
    );
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
        <form className="setup-form" noValidate autoComplete="off" onSubmit={submit}>
          <FieldGroup title="Company">
            {renderField("name", "Company name")}
            {renderField("contactEmail", "Contact email", { type: "email" })}
            {renderField("contactPhone", "Phone", { type: "tel" })}
          </FieldGroup>

          <FieldGroup title="Address">
            {renderField("addressLine1", "Address line 1", {
              className: "setup-field--wide",
            })}
            {renderField("addressLine2", "Address line 2 (optional)", {
              className: "setup-field--wide",
            })}
            <LocationFields
              state={values.state}
              city={values.city}
              stateError={errors.state}
              cityError={errors.city}
              stateRef={(node) => {
                fieldRefs.current.state = node;
              }}
              cityRef={(node) => {
                fieldRefs.current.city = node;
              }}
              onStateChange={(next) => updateField("state", next)}
              onCityChange={(next) => updateField("city", next)}
            />
            {renderField("postalCode", "Postal code")}
          </FieldGroup>

          <FileField
            id="company-logo"
            label="Company logo"
            hint="PNG, JPEG, or WebP. Maximum 2 MB."
            error={logoError || null}
            inputRef={logoRef}
            accept="image/png,image/jpeg,image/webp"
            onChange={(event) => selectLogo(event.target.files?.[0] ?? null)}
            preview={
              previewUrl || logoUrl ? (
                // eslint-disable-next-line @next/next/no-img-element
                <img className="setup-logo__preview" src={previewUrl ?? logoUrl ?? ""} alt="Company logo preview" />
              ) : (
                <div className="setup-logo__placeholder" aria-hidden="true">Logo</div>
              )
            }
          />

          {FIELD_ORDER.some((field) => Boolean(errors[field])) || logoError ? (
            <p className="setup-alert" role="alert">
              Review the highlighted fields. The first issue is{" "}
              {errors[FIELD_ORDER.find((field) => errors[field]) ?? "name"] ?? logoError}
            </p>
          ) : null}
          <ToastOutlet toast={toast} />

          <div className="setup-actions">
            <Button type="submit" loading={saving} loadingLabel="Saving…">
              Save and continue
            </Button>
          </div>
        </form>
      ) : null}
    </SetupWizardShell>
  );
}
