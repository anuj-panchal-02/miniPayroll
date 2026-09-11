"use client";

import { Choice } from "@/components/ui/Choice";
import { Field } from "@/components/ui/Field";

export type StatutoryPayrollValues = {
  pfApplicable: boolean;
  pfUseWageCeiling: boolean;
  esiApplicable: boolean;
  pfEstablishmentCode: string;
  esiCode: string;
};

type StatutoryPayrollFieldsProps = {
  values: StatutoryPayrollValues;
  companyState: string | null;
  onChange: (next: StatutoryPayrollValues) => void;
};

export function defaultStatutoryPayrollValues(
  source?: Partial<StatutoryPayrollValues> | null,
): StatutoryPayrollValues {
  return {
    pfApplicable: source?.pfApplicable ?? true,
    pfUseWageCeiling: source?.pfUseWageCeiling ?? true,
    esiApplicable: source?.esiApplicable ?? true,
    pfEstablishmentCode: source?.pfEstablishmentCode ?? "",
    esiCode: source?.esiCode ?? "",
  };
}

export function StatutoryPayrollFields({
  values,
  companyState,
  onChange,
}: StatutoryPayrollFieldsProps) {
  return (
    <fieldset className="setup-fieldset">
      <legend>Statutory deductions</legend>
      <p className="setup-field__hint">
        PF, ESI, professional tax, and labour welfare fund are calculated at payroll. Professional
        tax and LWF use the company state
        {companyState ? ` (${companyState})` : ""}. TDS stays a one-time amount on each run.
      </p>
      <div className="setup-rate-options">
        <Choice
          className="setup-rate"
          type="checkbox"
          checked={values.pfApplicable}
          onChange={(event) => onChange({ ...values, pfApplicable: event.target.checked })}
          label={
            <span>
              <strong>Provident Fund</strong>
              <small>Employee 12% of Basic + DA.</small>
            </span>
          }
        />
        {values.pfApplicable ? (
          <Choice
            className="setup-rate"
            type="checkbox"
            checked={values.pfUseWageCeiling}
            onChange={(event) => onChange({ ...values, pfUseWageCeiling: event.target.checked })}
            label={
              <span>
                <strong>Cap PF wages at ₹15,000</strong>
                <small>Turn off to contribute on full Basic + DA.</small>
              </span>
            }
          />
        ) : null}
        <Choice
          className="setup-rate"
          type="checkbox"
          checked={values.esiApplicable}
          onChange={(event) => onChange({ ...values, esiApplicable: event.target.checked })}
          label={
            <span>
              <strong>ESI</strong>
              <small>Employee 0.75% when the employee is covered.</small>
            </span>
          }
        />
      </div>
      <div className="setup-form">
        <Field
          id="pf-establishment"
          label="PF establishment code (optional)"
          hint="Printed on payslips. Not used in the formula."
        >
          <input
            className="mp-input"
            value={values.pfEstablishmentCode}
            maxLength={50}
            onChange={(event) =>
              onChange({ ...values, pfEstablishmentCode: event.target.value })
            }
          />
        </Field>
        <Field
          id="esi-code"
          label="ESI code (optional)"
          hint="Printed on payslips. Not used in the formula."
        >
          <input
            className="mp-input"
            value={values.esiCode}
            maxLength={50}
            onChange={(event) => onChange({ ...values, esiCode: event.target.value })}
          />
        </Field>
      </div>
    </fieldset>
  );
}
