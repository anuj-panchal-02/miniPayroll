import type { ReactNode } from "react";

type FieldGroupProps = {
  title: string;
  hint?: string;
  children: ReactNode;
  className?: string;
};

export function FieldGroup({ title, hint, children, className }: FieldGroupProps) {
  return (
    <section className={["mp-group", className].filter(Boolean).join(" ")}>
      <h2 className="mp-group__title">{title}</h2>
      {hint ? <p className="mp-group__hint">{hint}</p> : null}
      {children}
    </section>
  );
}
