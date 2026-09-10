"use client";

import type { InputHTMLAttributes, ReactNode, Ref } from "react";

type ChoiceProps = InputHTMLAttributes<HTMLInputElement> & {
  label: ReactNode;
  inputRef?: Ref<HTMLInputElement>;
};

export function Choice({ label, className, inputRef, ...props }: ChoiceProps) {
  return (
    <label className={["mp-choice", className].filter(Boolean).join(" ")}>
      <input ref={inputRef} {...props} />
      <span className="mp-choice__label">{label}</span>
    </label>
  );
}
