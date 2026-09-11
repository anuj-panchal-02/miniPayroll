"use client";

import { useRef, useState } from "react";
import type { EmployeeDetail, EmployeeInput, EmployeeStatus, Gender } from "@/lib/api";
import { EmployeeStatus as Status, Gender as GenderValue } from "@/lib/api";
import {
  SalaryStructureEditor,
  newSalaryStructureFields,
  salaryStructureError,
  toSalaryStructureInput,
  type SalaryStructureFields,
} from "@/components/SalaryStructureEditor";
import { useToast } from "@/components/Toast";
import { Button } from "@/components/ui/Button";
import { Choice } from "@/components/ui/Choice";
import { Field } from "@/components/ui/Field";
import { FieldGroup } from "@/components/ui/FieldGroup";
import { DateField } from "@/components/ui/DateField";
import { Select } from "@/components/ui/Select";
import { LocationFields } from "@/components/LocationFields";
import { Stepper } from "@/components/ui/Stepper";
import {
  BANK_FIELDS,
  employeeBankErrors,
  employeeDraftErrors,
  employeeErrors,
  employeePayrollErrors,
  employeePersonalErrors,
  PAYROLL_FIELDS,
  PERSONAL_FIELDS,
  type EmployeeErrors,
  type EmployeeField,
  type EmployeeFields,
} from "@/lib/validation";

const STEPS = ["Personal details", "Bank details", "Payroll details", "Salary structure"] as const;
const STEP_FIELDS = [PERSONAL_FIELDS, BANK_FIELDS, PAYROLL_FIELDS] as const;
const STEP_VALIDATORS = [
  employeePersonalErrors,
  employeeBankErrors,
  employeePayrollErrors,
] as const;

const PLACEHOLDERS: Partial<Record<EmployeeField, string>> = {
  employeeCode: "EMP-01",
  fullName: "Priya Sharma",
  email: "priya@company.example",
  phone: "9876543210",
  addressLine1: "12 MG Road",
  addressLine2: "Suite 4",
  city: "Pune",
  state: "Maharashtra",
  postalCode: "411001",
  designation: "Engineer",
  department: "Engineering",
  bankName: "HDFC Bank",
  bankAccountNumber: "123456789012",
  ifsc: "HDFC0001234",
  upiId: "priya@upi",
  overtimeRate: "200",
};

type EmployeeFormProps = {
  employee?: EmployeeDetail;
  submitLabel: string;
  onSave: (input: EmployeeInput) => Promise<void>;
};

export function EmployeeForm({ employee, submitLabel, onSave }: EmployeeFormProps) {
  const allowDraft = !employee || employee.status === Status.Draft;
  const collectSalary = !employee || employee.status === Status.Draft;
  const steps = collectSalary ? STEPS : STEPS.slice(0, 3);
  const [step, setStep] = useState(() =>
    allowDraft && employee?.draftStep ? clampStep(employee.draftStep) : 1,
  );
  const [values, setValues] = useState<EmployeeFields>(() => fromEmployee(employee));
  const [status, setStatus] = useState<EmployeeStatus>(
    employee?.status === Status.Inactive ? Status.Inactive : Status.Active,
  );
  const [errors, setErrors] = useState<EmployeeErrors>({});
  const [salary, setSalary] = useState<SalaryStructureFields>(() =>
    newSalaryStructureFields(employee?.joiningDate ?? today()),
  );
  const [salaryError, setSalaryError] = useState("");
  const [pending, setPending] = useState(false);
  const toast = useToast();
  const refs = useRef<Partial<Record<EmployeeField, HTMLElement | null>>>({});

  async function save(kind: "draft" | "complete") {
    const nextErrors =
      kind === "draft" ? employeeDraftErrors(values) : employeeErrors(values);
    setErrors(nextErrors);
    const invalid = [...PERSONAL_FIELDS, ...BANK_FIELDS, ...PAYROLL_FIELDS].find(
      (field) => nextErrors[field],
    );
    if (invalid) {
      const errorStep = STEP_FIELDS.findIndex((fields) => fields.includes(invalid)) + 1;
      if (errorStep) setStep(clampStep(errorStep));
      refs.current[invalid]?.focus();
      return;
    }

    if (kind === "complete" && collectSalary) {
      const message = salaryStructureError(salary, values.joiningDate);
      setSalaryError(message ?? "");
      if (message) {
        setStep(4);
        return;
      }
    }

    setPending(true);
    toast.dismiss();
    try {
      await onSave(
        toInput(
          values,
          status,
          kind === "draft",
          kind === "draft" ? step : null,
          kind === "complete" && collectSalary ? toSalaryStructureInput(salary) : null,
        ),
      );
      if (kind === "draft") {
        toast.showSuccess("Draft saved.");
      }
    } catch (reason) {
      toast.showError(reason instanceof Error ? reason.message : "Could not save the employee.");
    } finally {
      setPending(false);
    }
  }

  function goNext() {
    if (step === 4) return;
    const nextErrors = STEP_VALIDATORS[step - 1](values);
    setErrors(nextErrors);
    const invalid = STEP_FIELDS[step - 1].find((field) => nextErrors[field]);
    if (invalid) {
      refs.current[invalid]?.focus();
      return;
    }
    setStep((current) => clampStep(current + 1));
  }

  function updateField(name: EmployeeField, value: string) {
    setValues((current) => ({ ...current, [name]: value }));
    setErrors((current) => {
      if (!current[name]) {
        return current;
      }
      const next = { ...current };
      delete next[name];
      return next;
    });
  }

  const statusLabel = !employee
    ? "New"
    : employee.status === Status.Draft
      ? "Draft"
      : status === Status.Inactive
        ? "Inactive"
        : "Active";
  const nameFact = values.fullName.trim() || "—";
  const codeFact = values.employeeCode.trim() || "—";
  const stepTitle = steps[step - 1];
  const lastStep = step === steps.length;
  const commandLede = lastStep
    ? allowDraft
      ? "Save to finish, or keep a draft."
      : "Save to finish this employee."
    : allowDraft
      ? "Continue, or save a draft."
      : "Continue to the next step.";

  function field(name: EmployeeField, label: string, type = "text") {
    const message = errors[name];
    if (type === "date") {
      return (
        <Field id={name} label={label} error={message}>
          <DateField
            inputRef={(node) => {
              refs.current[name] = node;
            }}
            name={name}
            value={values[name]}
            onChange={(next) => updateField(name, next)}
          />
        </Field>
      );
    }
    return (
      <Field id={name} label={label} error={message}>
        <input
          ref={(node) => {
            refs.current[name] = node;
          }}
          className="mp-input"
          name={name}
          type={type}
          value={values[name]}
          placeholder={PLACEHOLDERS[name]}
          onChange={(event) => updateField(name, event.target.value)}
        />
      </Field>
    );
  }

  return (
    <form
      className="sa-compose"
      noValidate
      autoComplete="off"
      onSubmit={(event) => {
        event.preventDefault();
      }}
    >
      <div className="mp-kpi-grid" role="region" aria-label="Employee snapshot">
        <div className="mp-kpi-card">
          <span className="mp-kpi-card__label">Status</span>
          <span className="mp-kpi-card__value">{statusLabel}</span>
          <span className="mp-kpi-card__subtext">{stepTitle}</span>
        </div>
        <div className="mp-kpi-card">
          <span className="mp-kpi-card__label">Name</span>
          <span className="mp-kpi-card__value">{nameFact}</span>
          <span className="mp-kpi-card__subtext">Full name</span>
        </div>
        <div className="mp-kpi-card">
          <span className="mp-kpi-card__label">Code</span>
          <span className="mp-kpi-card__value">{codeFact}</span>
          <span className="mp-kpi-card__subtext">Employee ID</span>
        </div>
      </div>
      <Stepper
        className="sa-progress"
        label="Employee details"
        currentStep={step}
        steps={steps}
      />
      {step === 1 ? (
        <>
          <FieldGroup title="Identity">
            {field("employeeCode", "Employee ID")}
            {field("fullName", "Full name")}
            {field("email", "Email", "email")}
            {field("phone", "Phone")}
            <Field id="gender" label="Gender" error={errors.gender ?? null}>
              <Select
                value={values.gender}
                onChange={(next) => updateField("gender", next)}
                options={[
                  { value: "", label: "Select gender" },
                  { value: String(GenderValue.Male), label: "Male" },
                  { value: String(GenderValue.Female), label: "Female" },
                ]}
              />
            </Field>
          </FieldGroup>
          <FieldGroup title="Address">
            {field("addressLine1", "Address line 1")}
            <LocationFields
              state={values.state}
              city={values.city}
              stateError={errors.state}
              cityError={errors.city}
              stateRef={(node) => {
                refs.current.state = node;
              }}
              cityRef={(node) => {
                refs.current.city = node;
              }}
              onStateChange={(next) => updateField("state", next)}
              onCityChange={(next) => updateField("city", next)}
            />
            {field("postalCode", "Postal code")}
          </FieldGroup>
          <FieldGroup title="Employment">
            {field("designation", "Designation")}
            <Field id="employment-type" label="Employment type" previewState="disabled">
              <input className="mp-input" value="Full-time monthly salaried" disabled />
            </Field>
            {field("joiningDate", "Joining date", "date")}
            {employee && employee.status !== Status.Draft ? (
              <Field id="status" label="Status">
                <Select
                  value={String(status)}
                  onChange={(next) => setStatus(Number(next) as EmployeeStatus)}
                  options={[
                    { value: String(Status.Active), label: "Active" },
                    { value: String(Status.Inactive), label: "Inactive" },
                  ]}
                />
              </Field>
            ) : null}
          </FieldGroup>
          <details className="mp-disclose sa-compose__span">
            <summary>Optional details</summary>
            <div className="mp-disclose__body">
              {field("dateOfBirth", "Date of birth (optional)", "date")}
              {field("addressLine2", "Address line 2 (optional)")}
              {field("department", "Department (optional)")}
              {field("exitDate", "Exit date (optional)", "date")}
            </div>
          </details>
        </>
      ) : null}
      {step === 2 ? (
        <FieldGroup title="Bank">
          {field("bankName", "Bank name")}
          {field("bankAccountNumber", "Bank account number")}
          {field("ifsc", "IFSC")}
          <details className="mp-disclose sa-compose__span">
            <summary>Optional payment details</summary>
            <div className="mp-disclose__body">{field("upiId", "UPI ID (optional)")}</div>
          </details>
        </FieldGroup>
      ) : null}
      {step === 3 ? (
        <FieldGroup
          title="Payroll details"
          hint="Coverage inherits company defaults. Statutory amounts are calculated when you run payroll."
        >
          {field("overtimeRate", "Overtime rate (optional)", "number")}
          <Choice
            type="checkbox"
            checked={values.pfCovered === "true"}
            onChange={(event) => updateField("pfCovered", event.target.checked ? "true" : "false")}
            label="Covered by Provident Fund"
          />
          <Choice
            type="checkbox"
            checked={values.esiCovered === "true"}
            onChange={(event) => updateField("esiCovered", event.target.checked ? "true" : "false")}
            label="Covered by ESI"
          />
          {field("uan", "UAN (optional)")}
          {field("pfNumber", "PF number (optional)")}
          {field("esiNumber", "ESI number (optional)")}
        </FieldGroup>
      ) : null}
      {step === 4 ? (
        <SalaryStructureEditor
          value={salary}
          joiningDate={values.joiningDate}
          error={salaryError}
          onChange={(next) => {
            setSalary(next);
            setSalaryError("");
          }}
        />
      ) : null}
      <section className="sa-payroll-card" aria-label={stepTitle}>
        <h2 className="sa-payroll-card__title">{stepTitle}</h2>
        <p className="sa-payroll-card__lede">{commandLede}</p>
        <div className="sa-payroll-card__actions">
          {step > 1 ? (
            <Button
              type="button"
              variant="secondary"
              disabled={pending}
              onClick={() => {
                setStep((current) => clampStep(current - 1));
              }}
            >
              Back
            </Button>
          ) : null}
          {step < steps.length ? (
            <Button type="button" disabled={pending} onClick={goNext}>
              Next
            </Button>
          ) : (
            <Button
              type="button"
              loading={pending}
              loadingLabel="Saving…"
              onClick={() => void save("complete")}
            >
              {submitLabel}
            </Button>
          )}
          {allowDraft ? (
            <Button
              type="button"
              variant="secondary"
              disabled={pending}
              onClick={() => void save("draft")}
            >
              Save as draft
            </Button>
          ) : null}
        </div>
      </section>
    </form>
  );
}

function clampStep(step: number): 1 | 2 | 3 | 4 {
  if (step <= 1) return 1;
  if (step === 2) return 2;
  if (step === 3) return 3;
  return 4;
}

function fromEmployee(employee?: EmployeeDetail): EmployeeFields {
  return {
    employeeCode: employee?.employeeCode ?? "",
    fullName: employee?.fullName ?? "",
    dateOfBirth: employee?.dateOfBirth ?? "",
    phone: employee?.phone ?? "",
    email: employee?.email ?? "",
    addressLine1: employee?.addressLine1 ?? "",
    addressLine2: employee?.addressLine2 ?? "",
    city: employee?.city ?? "",
    state: employee?.state ?? "",
    postalCode: employee?.postalCode ?? "",
    designation: employee?.designation ?? "",
    department: employee?.department ?? "",
    joiningDate: employee?.joiningDate ?? "",
    exitDate: employee?.exitDate ?? "",
    bankName: employee?.bankName ?? "",
    bankAccountNumber: employee?.bankAccountNumber ?? "",
    ifsc: employee?.ifsc ?? "",
    upiId: employee?.upiId ?? "",
    overtimeRate:
      employee?.overtimeRate == null ? "" : String(employee.overtimeRate),
    gender: employee?.gender == null ? "" : String(employee.gender),
    pfCovered: employee?.pfCovered === false ? "false" : "true",
    esiCovered: employee?.esiCovered === false ? "false" : "true",
    uan: employee?.uan ?? "",
    pfNumber: employee?.pfNumber ?? "",
    esiNumber: employee?.esiNumber ?? "",
  };
}

function toInput(
  values: EmployeeFields,
  status: EmployeeStatus,
  saveAsDraft: boolean,
  draftStep: number | null,
  salaryStructure: EmployeeInput["salaryStructure"],
): EmployeeInput {
  const overtime = values.overtimeRate.trim();
  return {
    employeeCode: values.employeeCode.trim(),
    fullName: values.fullName.trim(),
    dateOfBirth: values.dateOfBirth || null,
    phone: values.phone.trim(),
    email: values.email.trim(),
    addressLine1: values.addressLine1.trim(),
    addressLine2: values.addressLine2.trim() || null,
    city: values.city.trim(),
    state: values.state.trim(),
    postalCode: values.postalCode.trim(),
    designation: values.designation.trim(),
    department: values.department.trim() || null,
    joiningDate: values.joiningDate || null,
    exitDate: values.exitDate || null,
    status,
    bankName: values.bankName.trim(),
    bankAccountNumber: values.bankAccountNumber.replace(/\D/g, ""),
    ifsc: values.ifsc.trim().toUpperCase(),
    upiId: values.upiId.trim() || null,
    overtimeRate: overtime ? Number(overtime) : null,
    gender: values.gender === "" ? null : (Number(values.gender) as Gender),
    pfCovered: values.pfCovered !== "false",
    esiCovered: values.esiCovered !== "false",
    uan: values.uan.replace(/\D/g, "") || null,
    pfNumber: values.pfNumber.trim() || null,
    esiNumber: values.esiNumber.trim() || null,
    saveAsDraft,
    draftStep,
    salaryStructure,
  };
}

function today(): string {
  return new Date().toISOString().slice(0, 10);
}
