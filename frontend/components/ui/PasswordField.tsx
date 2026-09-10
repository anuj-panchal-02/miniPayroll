"use client";

import { useState, type ChangeEvent, type InputHTMLAttributes, type Ref } from "react";

import { passwordStrength } from "@/lib/validation";

import { PasswordStrength } from "./PasswordStrength";

type PasswordFieldProps = Omit<InputHTMLAttributes<HTMLInputElement>, "id" | "type"> & {
  id: string;
  label: string;
  hint?: string;
  error?: string | null;
  showStrength?: boolean;
  inputRef?: Ref<HTMLInputElement>;
};

export function PasswordField({
  id,
  label,
  hint,
  error,
  required,
  className,
  showStrength = true,
  inputRef,
  ...inputProps
}: PasswordFieldProps) {
  const [visible, setVisible] = useState(false);
  const [typed, setTyped] = useState("");
  const slotText = error ?? hint ?? "";
  const value = typeof inputProps.value === "string" ? inputProps.value : typed;
  const meterId = `${id}-strength`;
  const strength = showStrength && value.length > 0 ? passwordStrength(value) : null;
  const showMeter = strength !== null;
  const describedBy =
    [error ? `${id}-error` : hint ? `${id}-hint` : null, showMeter ? meterId : null]
      .filter(Boolean)
      .join(" ") || undefined;

  function handleChange(event: ChangeEvent<HTMLInputElement>) {
    setTyped(event.target.value);
    inputProps.onChange?.(event);
  }

  return (
    <div className={["mp-field", className].filter(Boolean).join(" ")}>
      <label htmlFor={id} className="mp-field__label">
        {label}
      </label>
      <div className={["mp-field__well", "mp-field__well--icon", error ? "is-error" : ""]
        .filter(Boolean)
        .join(" ")}
      >
        <div className="mp-field__control mp-password">
          <input
            {...inputProps}
            ref={inputRef}
            id={id}
            className="mp-input"
            type={visible ? "text" : "password"}
            autoComplete={inputProps.autoComplete ?? "current-password"}
            aria-invalid={error ? true : undefined}
            aria-describedby={describedBy}
            aria-required={required || undefined}
            onChange={handleChange}
          />
          <button
            type="button"
            className="mp-password__toggle"
            onClick={() => setVisible((current) => !current)}
            aria-pressed={visible}
            aria-label={visible ? "Hide password" : "Show password"}
          >
            {visible ? "Hide" : "Show"}
          </button>
        </div>
      </div>
      {strength ? (
        <PasswordStrength
          id={meterId}
          strength={strength}
          showAdvice={strength.advice !== slotText}
        />
      ) : null}
      <p
        id={error ? `${id}-error` : `${id}-hint`}
        className="mp-field__slot"
        data-tone={error ? "error" : undefined}
      >
        {slotText}
      </p>
    </div>
  );
}
