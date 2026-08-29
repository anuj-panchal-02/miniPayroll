"use client";

import {
  SalaryComponentType,
  SalaryComponentValueType,
  type SalaryStructureInput,
} from "@/lib/api";

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
      <label className="sa-field">
        Effective from
        <input
          type="date"
          min={joiningDate || undefined}
          value={value.effectiveFrom}
          onChange={(event) => onChange({ ...value, effectiveFrom: event.target.value })}
          aria-invalid={dateError ? true : undefined}
          aria-describedby={dateError ? "salary-date-error" : undefined}
        />
        <span
          id="salary-date-error"
          className="sa-field__error"
          role={dateError ? "alert" : undefined}
        >
          {dateError}
        </span>
      </label>
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
        const amountInvalid = Boolean(valueError) && index === value.components.length - 1;
        return (
          <div className="sa-salary-fields" key={component.key}>
            <label className="sa-field">
              Name
              <input
                value={component.name}
                readOnly={basic}
                onChange={(event) => update(index, { name: event.target.value })}
              />
              <span className="sa-field__error" aria-hidden="true" />
            </label>
            <label className="sa-field">
              Type
              <select
                value={component.type}
                disabled={basic}
                onChange={(event) => update(index, { type: Number(event.target.value) as SalaryComponentType })}
              >
                <option value={SalaryComponentType.Earning}>Earning</option>
                <option value={SalaryComponentType.Deduction}>Deduction</option>
              </select>
              <span className="sa-field__error" aria-hidden="true" />
            </label>
            <label className="sa-field">
              Value type
              <select
                value={component.valueType}
                disabled={basic}
                onChange={(event) => update(index, {
                  valueType: Number(event.target.value) as SalaryComponentValueType,
                })}
              >
                <option value={SalaryComponentValueType.FixedAmount}>Fixed amount (₹)</option>
                <option value={SalaryComponentValueType.PercentageOfBasic}>% of Basic Salary</option>
              </select>
              <span className="sa-field__error" aria-hidden="true" />
            </label>
            <label className="sa-field">
              Amount
              <input
                type="number"
                min="0.01"
                step="0.01"
                placeholder="25000"
                value={component.value}
                onChange={(event) => update(index, { value: event.target.value })}
                aria-invalid={amountInvalid ? true : undefined}
                aria-describedby={amountInvalid ? "salary-amount-error" : undefined}
              />
              <span
                id={amountInvalid ? "salary-amount-error" : undefined}
                className="sa-field__error"
                role={amountInvalid ? "alert" : undefined}
              >
                {amountInvalid ? valueError : ""}
              </span>
            </label>
            {!basic ? (
              <button
                type="button"
                className="sa-compose__remove"
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

function newComponent(name: string, type: SalaryComponentType): ComponentDraft {
  return {
    key: `${name}-${globalThis.crypto?.randomUUID?.() ?? Math.random().toString(36).slice(2)}`,
    name,
    type,
    valueType: SalaryComponentValueType.FixedAmount,
    value: "",
  };
}
