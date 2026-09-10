"use client";

import { useState } from "react";

import { DateField } from "./DateField";
import { Field, type FieldPreviewState } from "./Field";
import { PasswordField } from "./PasswordField";
import { Select } from "./Select";

const STATES: { label: string; state?: FieldPreviewState }[] = [
  { label: "default" },
  { label: "hover", state: "hover" },
  { label: "focus", state: "focus" },
  { label: "active", state: "active" },
  { label: "inactive", state: "inactive" },
  { label: "disabled", state: "disabled" },
  { label: "loading", state: "loading" },
  { label: "error", state: "error" },
  { label: "success", state: "success" },
];

const PASSWORDS: { id: string; label: string; value: string }[] = [
  { id: "weak", label: "too weak", value: "design" },
  { id: "improving", label: "improving", value: "Design.dey" },
  { id: "strong", label: "strong", value: "Design.dey123!" },
];

const rowStyle = {
  display: "grid",
  gridTemplateColumns: "8rem minmax(0, 1fr)",
  alignItems: "start",
  gap: "var(--space-md)",
} as const;

const rowLabelStyle = {
  fontFamily: "var(--font-mono)",
  fontSize: "0.75rem",
  color: "var(--color-neutral)",
  paddingTop: "var(--space-sm)",
} as const;

const listStyle = {
  marginTop: "var(--space-lg)",
  display: "grid",
  gap: "var(--space-md)",
  listStyle: "none",
  padding: 0,
  maxWidth: "26rem",
} as const;

const captionStyle = {
  fontSize: "var(--text-sm)",
  color: "var(--color-muted)",
} as const;

function PreviewSelect() {
  const [value, setValue] = useState("0");
  return (
    <Field id="preview-select" label="Status" hint="Active employees appear on payroll.">
      <Select
        value={value}
        onChange={setValue}
        options={[
          { value: "0", label: "Active" },
          { value: "1", label: "Inactive" },
        ]}
      />
    </Field>
  );
}

function PreviewDate() {
  const [value, setValue] = useState("2026-01-15");
  return (
    <Field id="preview-date" label="Joining date" hint="Used as the salary effective-from floor.">
      <DateField value={value} min="2026-01-01" onChange={setValue} />
    </Field>
  );
}

function PreviewPassword({ id, initial }: { id: string; initial: string }) {
  const [value, setValue] = useState(initial);
  return (
    <PasswordField
      id={`preview-password-${id}`}
      label="Password"
      value={value}
      onChange={(event) => setValue(event.target.value)}
      autoComplete="new-password"
    />
  );
}

export function FieldPreview() {
  return (
    <main
      style={{
        minHeight: "100vh",
        padding: "var(--space-xl)",
        background: "var(--color-paper)",
        color: "var(--color-ink)",
        fontFamily: "var(--font-body)",
      }}
    >
      <p style={captionStyle}>Field — 9 states</p>
      <ol style={listStyle}>
        {STATES.map((row) => (
          <li key={row.label} style={rowStyle}>
            <span style={rowLabelStyle}>{row.label}</span>
            <Field
              id={`preview-${row.label}`}
              label="Work email"
              hint="Used for payslips."
              previewState={row.state}
            >
              <input
                className="mp-input"
                defaultValue={row.state === "disabled" ? "" : "ada@example.com"}
              />
            </Field>
          </li>
        ))}
        <li style={rowStyle}>
          <span style={rowLabelStyle}>read-only</span>
          <Field id="preview-readonly" label="Company slug" hint="Generated from the name.">
            <input className="mp-input" defaultValue="northwind-labs" readOnly />
          </Field>
        </li>
        <li style={rowStyle}>
          <span style={rowLabelStyle}>affix</span>
          <Field id="preview-affix" label="Amount" affix="₹" hint="Fixed monthly amount.">
            <input className="mp-input" type="number" defaultValue="25000" data-numeric />
          </Field>
        </li>
      </ol>

      <p style={{ ...captionStyle, marginTop: "var(--space-xl)" }}>
        Password — strength meter (type to watch it move; click Show to reveal)
      </p>
      <ol style={listStyle}>
        {PASSWORDS.map((row) => (
          <li key={row.id} style={rowStyle}>
            <span style={rowLabelStyle}>{row.label}</span>
            <PreviewPassword id={row.id} initial={row.value} />
          </li>
        ))}
        <li style={rowStyle}>
          <span style={rowLabelStyle}>meter off</span>
          <PasswordField
            id="preview-password-off"
            label="Password"
            defaultValue="Design.dey123!"
            showStrength={false}
            hint="Strength meter suppressed."
          />
        </li>
      </ol>

      <p style={{ ...captionStyle, marginTop: "var(--space-xl)" }}>
        Select and date — themed popovers, not OS chrome
      </p>
      <ol style={listStyle}>
        <li style={rowStyle}>
          <span style={rowLabelStyle}>dropdown</span>
          <PreviewSelect />
        </li>
        <li style={rowStyle}>
          <span style={rowLabelStyle}>date</span>
          <PreviewDate />
        </li>
      </ol>
    </main>
  );
}
