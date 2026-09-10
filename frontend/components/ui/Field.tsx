"use client";

import { cloneElement, isValidElement, type ReactElement, type ReactNode } from "react";

export type FieldPreviewState =
  | "hover"
  | "focus"
  | "active"
  | "inactive"
  | "disabled"
  | "loading"
  | "error"
  | "success";

type FieldProps = {
  id: string;
  label: string;
  children: ReactNode;
  hint?: string;
  error?: string | null;
  success?: string | null;
  optional?: boolean;
  required?: boolean;
  affix?: ReactNode;
  meter?: ReactNode;
  className?: string;
  previewState?: FieldPreviewState;
};

const PREVIEW_CLASS: Record<FieldPreviewState, string> = {
  hover: "is-hover",
  focus: "is-focus",
  active: "is-active",
  inactive: "is-inactive",
  disabled: "is-disabled",
  loading: "is-loading",
  error: "is-error",
  success: "is-success",
};

export function Field({
  id,
  label,
  children,
  hint,
  error,
  success,
  optional,
  required,
  affix,
  meter,
  className,
  previewState,
}: FieldProps) {
  const slotError = error || (previewState === "error" ? "Enter a valid value." : null);
  const slotSuccess = slotError
    ? null
    : success || (previewState === "success" ? "Looks good." : null);
  const slotText = slotError ?? slotSuccess ?? hint ?? "";
  const describedBy = slotError
    ? `${id}-error`
    : hint || slotSuccess
      ? `${id}-hint`
      : undefined;
  const child = isValidElement(children)
    ? (children as ReactElement<Record<string, unknown>>)
    : null;
  const control = child
    ? cloneElement(child, {
        id,
        className: child.props.className,
        "aria-invalid": slotError ? true : undefined,
        "aria-describedby": describedBy,
        "aria-required": required || undefined,
        disabled: previewState === "disabled" || child.props.disabled,
        autoComplete: child.props.autoComplete ?? "off",
      })
    : children;

  const wellClass = [
    "mp-field__well",
    affix ? "mp-field__well--affix" : "",
    previewState ? PREVIEW_CLASS[previewState] : "",
    slotError ? "is-error" : "",
    slotSuccess ? "is-success" : "",
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <div className={["mp-field", className].filter(Boolean).join(" ")}>
      <label htmlFor={id} className="mp-field__label">
        {label}
        {optional ? <span className="mp-field__optional">(optional)</span> : null}
      </label>
      <div className={wellClass}>
        <div className={["mp-field__control", affix ? "mp-affix" : ""].filter(Boolean).join(" ")}>
          {affix ? (
            <span className="mp-affix__mark" aria-hidden="true">
              {affix}
            </span>
          ) : null}
          {control}
          {previewState === "loading" ? (
            <span className="mp-field__spinner" aria-hidden="true" />
          ) : null}
        </div>
      </div>
      {meter}
      <p
        id={slotError ? `${id}-error` : `${id}-hint`}
        className="mp-field__slot"
        data-tone={slotError ? "error" : slotSuccess ? "success" : undefined}
      >
        {slotText}
      </p>
    </div>
  );
}
