"use client";

import {
  SalaryComponentType,
  SalaryComponentValueType,
  type SalaryStructureInput,
} from "@/lib/api";
import { Field } from "@/components/ui/Field";
import { DateField } from "@/components/ui/DateField";
import { Select } from "@/components/ui/Select";

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
  { name: "HRA", type: SalaryComponentType.Earning },
  { name: "Conveyance Allowance", type: SalaryComponentType.Earning },
  { name: "Special Allowance", type: SalaryComponentType.Earning },
  { name: "Provident Fund (PF)", type: SalaryComponentType.Deduction },
  { name: "ESI", type: SalaryComponentType.Deduction },
  { name: "Professional Tax", type: SalaryComponentType.Deduction },
  { name: "Labour Welfare Fund (LWF)", type: SalaryComponentType.Deduction },
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
    if (usedNames.has(duplicateKey)) return "Each earning or deduction can be added only once.";
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
      <p className="sa-muted">Recurring lines only. One-time changes are added when payroll is run.</p>
      <div className="sa-preset-actions" aria-label="Add standard salary component">
        {PRESETS.filter((preset) => !presetAdded(preset)).map((preset) => (
          <button
            type="button"
            className="sa-compose__add"
            key={preset.name}
            onClick={() => add(preset.name, preset.type)}
          >
            + {preset.name}
          </button>
        ))}
        <button type="button" className="sa-compose__add" onClick={() => add()}>
          + Custom line
        </button>
      </div>
      {value.components.map((component, index) => {
        const basic = component.name.trim().toLowerCase() === "basic salary";
        const percent = component.valueType === SalaryComponentValueType.PercentageOfBasic;
        const rowError = rowMessage(component, Boolean(valueError), index === value.components.length - 1 ? valueError : "");
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
                  { value: String(SalaryComponentType.Deduction), label: "Deduction" },
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
              <button
                type="button"
                className="sa-compose__remove"
                aria-label={`Remove ${component.name || "component"}`}
                onClick={() => onChange({
                  ...value,
                  components: value.components.filter((_, current) => current !== index),
                })}
              >
                Remove
              </button>
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
