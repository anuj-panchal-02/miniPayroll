"use client";

import {
  SalaryComponentType,
  SalaryComponentValueType,
  type SalaryStructureInput,
} from "@/lib/api";
import { Field } from "@/components/ui/Field";
import { DateField } from "@/components/ui/DateField";
import { Select } from "@/components/ui/Select";
import { Button } from "@/components/ui/Button";
import { formatRupees } from "@/lib/payroll";

type ComponentDraft = {
  key: string;
  name: string;
  type: SalaryComponentType;
  valueType: SalaryComponentValueType;
  value: string;
};

export type SalaryStructureFields = {
  effectiveFrom: string;
  components: ComponentDraft[];
};

const PRESETS: Array<{ name: string; type: SalaryComponentType }> = [
  { name: "Dearness Allowance (DA)", type: SalaryComponentType.Earning },
  { name: "HRA", type: SalaryComponentType.Earning },
  { name: "Conveyance Allowance", type: SalaryComponentType.Earning },
  { name: "Special Allowance", type: SalaryComponentType.Earning },
];

export function newSalaryStructureFields(effectiveFrom = ""): SalaryStructureFields {
  return {
    effectiveFrom,
    components: [newComponent("Basic Salary", SalaryComponentType.Earning)],
  };
}

export function toSalaryStructureInput(fields: SalaryStructureFields): SalaryStructureInput {
  return {
    effectiveFrom: fields.effectiveFrom,
    components: fields.components.map((component, sortOrder) => ({
      name: component.name.trim(),
      type: component.type,
      valueType: component.valueType,
      value: Number(component.value),
      sortOrder,
    })),
  };
}

export function salaryStructureFieldsFrom(
  input: SalaryStructureInput,
): SalaryStructureFields {
  return {
    effectiveFrom: input.effectiveFrom,
    components: input.components.map((component) => ({
      key: `${component.name}-${component.sortOrder}`,
      name: component.name,
      type: component.type,
      valueType: component.valueType,
      value: String(component.value),
    })),
  };
}

export function salaryStructureError(fields: SalaryStructureFields, joiningDate: string): string | null {
  if (!fields.effectiveFrom) return "Enter an effective date.";
  if (joiningDate && fields.effectiveFrom < joiningDate) {
    return "The salary effective date cannot be before the joining date.";
  }
  const basic = fields.components.filter((component) =>
    component.name.trim().toLowerCase() === "basic salary",
  );
  if (
    basic.length !== 1 ||
    basic[0].type !== SalaryComponentType.Earning ||
    basic[0].valueType !== SalaryComponentValueType.FixedAmount
  ) {
    return "Include exactly one fixed Basic Salary earning.";
  }
  const usedNames = new Set<string>();
  for (const component of fields.components) {
    const name = component.name.trim();
    const value = Number(component.value);
    const duplicateKey = `${component.type}:${name.toLowerCase()}`;
    if (
      !name ||
      name.length > 100 ||
      !Number.isFinite(value) ||
      value <= 0 ||
      (component.valueType === SalaryComponentValueType.PercentageOfBasic && value > 100)
    ) {
      return "Every salary component needs a name and a positive value.";
    }
    if (component.type !== SalaryComponentType.Earning) {
      return "Salary structure only includes earnings. Statutory deductions are calculated at payroll.";
    }
    usedNames.add(duplicateKey);
  }
  return null;
}

export function SalaryStructureEditor({
  value,
  onChange,
  joiningDate,
  error,
}: {
  value: SalaryStructureFields;
  onChange: (value: SalaryStructureFields) => void;
  joiningDate: string;
  error?: string;
}) {
  const dateError =
    error && /effective|joining date/i.test(error) ? error : "";
  const valueError = error && !dateError ? error : "";

  function update(index: number, patch: Partial<ComponentDraft>) {
    onChange({
      ...value,
      components: value.components.map((component, current) =>
        current === index ? { ...component, ...patch } : component,
      ),
    });
  }

  function add(name = "", type: SalaryComponentType = SalaryComponentType.Earning) {
    if (value.components.some((component) => component.name === name && component.type === type)) {
      return;
    }
    onChange({ ...value, components: [...value.components, newComponent(name, type)] });
  }

  function presetAdded(preset: (typeof PRESETS)[number]) {
    return value.components.some(
      (component) => component.name === preset.name && component.type === preset.type,
    );
  }

  // Real-time calculation preview
  const basicComp = value.components.find((c) => c.name.trim().toLowerCase() === "basic salary");
  const basicSalary = basicComp ? Math.max(0, Number(basicComp.value) || 0) : 0;

  let gross = 0;
  let deductions = 0;
  for (const comp of value.components) {
    const val = Number(comp.value) || 0;
    const amount =
      comp.valueType === SalaryComponentValueType.PercentageOfBasic
        ? Math.max(0, (val / 100) * basicSalary)
        : Math.max(0, val);

    if (comp.type === SalaryComponentType.Earning) {
      gross += amount;
    } else {
      deductions += amount;
    }
  }
  const netTakeHome = Math.max(0, gross - deductions);
  const basicRatio = gross > 0 ? Math.round((basicSalary / gross) * 100) : 0;

  return (
    <section className="sa-compose__salary" aria-label="Salary structure">
      <Field
        id="salary-effective-from"
        label="Effective from"
        error={dateError || null}
      >
        <DateField
          value={value.effectiveFrom}
          min={joiningDate || undefined}
          onChange={(effectiveFrom) => onChange({ ...value, effectiveFrom })}
        />
      </Field>

      <p className="sa-muted">
        Recurring earnings only. PF, ESI, professional tax, and LWF are calculated at payroll from
        company settings. One-time changes are added when payroll is run.
      </p>

      <div className="mp-kpi-grid" role="region" aria-label="Salary snapshot">
        <div className="mp-kpi-card">
          <span className="mp-kpi-card__label">Gross</span>
          <span className="mp-kpi-card__value">{formatRupees(gross)}</span>
          <span className="mp-kpi-card__subtext">
            {gross > 0 ? `Basic is ${basicRatio}% of gross` : "Monthly earnings"}
          </span>
        </div>
        <div className="mp-kpi-card">
          <span className="mp-kpi-card__label">Deductions</span>
          <span className="mp-kpi-card__value">{formatRupees(deductions)}</span>
          <span className="mp-kpi-card__subtext">Statutory lines are calculated later</span>
        </div>
        <div className="mp-kpi-card">
          <span className="mp-kpi-card__label">Take-home</span>
          <span className="mp-kpi-card__value">{formatRupees(netTakeHome)}</span>
          <span className="mp-kpi-card__subtext">Gross minus deductions</span>
        </div>
      </div>

      <div className="sa-preset-actions" aria-label="Add standard salary component">
        {PRESETS.filter((preset) => !presetAdded(preset)).map((preset) => (
          <button
            type="button"
            className="sa-compose__add"
            key={preset.name}
            onClick={() => add(preset.name, preset.type)}
          >
            {preset.name}
          </button>
        ))}
        <button type="button" className="sa-compose__add" onClick={() => add()}>
          Add custom line
        </button>
      </div>

      {value.components.map((component, index) => {
        const basic = component.name.trim().toLowerCase() === "basic salary";
        const percent = component.valueType === SalaryComponentValueType.PercentageOfBasic;
        const rowError = rowMessage(
          component,
          Boolean(valueError),
          index === value.components.length - 1 ? valueError : "",
        );

        return (
          <div className="sa-salary-fields" key={component.key}>
            <Field id={`salary-name-${component.key}`} label="Name">
              <input
                className="mp-input"
                value={component.name}
                readOnly={basic}
                onChange={(event) => update(index, { name: event.target.value })}
              />
            </Field>
            <Field id={`salary-type-${component.key}`} label="Type">
              <Select
                value={String(component.type)}
                disabled={basic}
                onChange={(next) => update(index, { type: Number(next) as SalaryComponentType })}
                options={[
                  { value: String(SalaryComponentType.Earning), label: "Earning" },
                ]}
              />
            </Field>
            <Field id={`salary-value-type-${component.key}`} label="Value type">
              <Select
                value={String(component.valueType)}
                disabled={basic}
                onChange={(next) =>
                  update(index, { valueType: Number(next) as SalaryComponentValueType })
                }
                options={[
                  { value: String(SalaryComponentValueType.FixedAmount), label: "Fixed amount (₹)" },
                  { value: String(SalaryComponentValueType.PercentageOfBasic), label: "% of Basic Salary" },
                ]}
              />
            </Field>
            <Field
              id={`salary-amount-${component.key}`}
              label="Amount"
              affix={percent ? "%" : "₹"}
              error={rowError || null}
            >
              <input
                className="mp-input"
                type="number"
                min="0.01"
                step="0.01"
                placeholder="25000"
                value={component.value}
                data-numeric
                onChange={(event) => update(index, { value: event.target.value })}
              />
            </Field>
            {!basic ? (
              <Button
                type="button"
                variant="ghost"
                aria-label={`Remove ${component.name || "component"}`}
                onClick={() =>
                  onChange({
                    ...value,
                    components: value.components.filter((_, current) => current !== index),
                  })
                }
              >
                Remove
              </Button>
            ) : null}
          </div>
        );
      })}
    </section>
  );
}

function rowMessage(component: ComponentDraft, show: boolean, fallback: string): string {
  if (!show) return "";
  const name = component.name.trim();
  const value = Number(component.value);
  if (!name || name.length > 100) return "Enter a component name.";
  if (!Number.isFinite(value) || value <= 0) {
    return fallback || "Every salary component needs a name and a positive value.";
  }
  if (component.valueType === SalaryComponentValueType.PercentageOfBasic && value > 100) {
    return fallback || "Every salary component needs a name and a positive value.";
  }
  return fallback;
}

function newComponent(name: string, type: SalaryComponentType): ComponentDraft {
  return {
    key: `${name}-${globalThis.crypto?.randomUUID?.() ?? Math.random().toString(36).slice(2)}`,
    name,
    type,
    valueType: SalaryComponentValueType.FixedAmount,
    value: "",
  };
}
