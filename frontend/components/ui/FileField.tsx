"use client";

import { useState, type ChangeEvent, type InputHTMLAttributes, type ReactNode, type Ref } from "react";

type FileFieldProps = Omit<InputHTMLAttributes<HTMLInputElement>, "id" | "type"> & {
  id: string;
  label: string;
  hint?: string;
  error?: string | null;
  preview?: ReactNode;
  inputRef?: Ref<HTMLInputElement>;
};

export function FileField({
  id,
  label,
  hint,
  error,
  preview,
  inputRef,
  className,
  onChange,
  ...inputProps
}: FileFieldProps) {
  const [fileName, setFileName] = useState("");
  const slotText = error ?? hint ?? "";
  const describedBy = error ? `${id}-error` : hint ? `${id}-hint` : undefined;

  function handleChange(event: ChangeEvent<HTMLInputElement>) {
    setFileName(event.target.files?.[0]?.name ?? "");
    onChange?.(event);
  }

  return (
    <div className={["setup-logo", className].filter(Boolean).join(" ")}>
      <div className="mp-field">
        <label htmlFor={id} className="mp-field__label">
          {label}
        </label>
        <div
          className={["mp-field__well", "mp-file-well", error ? "is-error" : ""]
            .filter(Boolean)
            .join(" ")}
        >
          <div className="mp-field__control">
            <span className="mp-file__value">{fileName || "Choose a file"}</span>
            <input
              {...inputProps}
              ref={inputRef}
              id={id}
              className="mp-file"
              type="file"
              aria-invalid={error ? true : undefined}
              aria-describedby={describedBy}
              onChange={handleChange}
            />
          </div>
        </div>
        <p
          id={error ? `${id}-error` : `${id}-hint`}
          className="mp-field__slot"
          data-tone={error ? "error" : undefined}
        >
          {slotText}
        </p>
      </div>
      {preview}
    </div>
  );
}
