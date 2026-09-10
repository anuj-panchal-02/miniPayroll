"use client";

import type { ButtonHTMLAttributes } from "react";

type ButtonVariant = "primary" | "secondary" | "destructive" | "ghost";

type ButtonProps = ButtonHTMLAttributes<HTMLButtonElement> & {
  variant?: ButtonVariant;
  loading?: boolean;
  loadingLabel?: string;
};

export function Button({
  variant = "primary",
  loading,
  loadingLabel,
  className,
  disabled,
  children,
  ...props
}: ButtonProps) {
  const classes = [
    "mp-btn",
    `mp-btn--${variant}`,
    loading ? "is-loading" : "",
    className,
  ]
    .filter(Boolean)
    .join(" ");

  return (
    <button
      {...props}
      className={classes}
      disabled={disabled || loading}
      aria-busy={loading || undefined}
    >
      {loading ? <span className="mp-btn__spinner" aria-hidden="true" /> : null}
      <span>{loading && loadingLabel ? loadingLabel : children}</span>
    </button>
  );
}

export function buttonClass(variant: ButtonVariant = "primary", extra?: string) {
  return ["mp-btn", `mp-btn--${variant}`, extra].filter(Boolean).join(" ");
}

export type { ButtonVariant };
