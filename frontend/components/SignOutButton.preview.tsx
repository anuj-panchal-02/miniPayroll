"use client";

import { SignOutButton, SignOutPreviewState } from "./SignOutButton";

const STATES: { label: string; state?: SignOutPreviewState }[] = [
  { label: "default" },
  { label: "hover", state: "hover" },
  { label: "focus", state: "focus" },
  { label: "active", state: "active" },
  { label: "disabled", state: "disabled" },
  { label: "loading", state: "loading" },
  { label: "error", state: "error" },
  { label: "success", state: "success" },
];

export function SignOutButtonPreview() {
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
      <p style={{ fontSize: "var(--text-sm)", color: "var(--color-muted)" }}>
        Sign out — 8 states
      </p>
      <ol
        style={{
          marginTop: "var(--space-lg)",
          display: "grid",
          gap: "var(--space-md)",
          listStyle: "none",
          padding: 0,
        }}
      >
        {STATES.map((row) => (
          <li
            key={row.label}
            style={{
              display: "grid",
              gridTemplateColumns: "7rem 1fr",
              alignItems: "center",
              gap: "var(--space-md)",
            }}
          >
            <span
              style={{
                fontFamily: "var(--font-mono)",
                fontSize: "0.75rem",
                color: "var(--color-neutral)",
              }}
            >
              {row.label}
            </span>
            <SignOutButton previewState={row.state} />
          </li>
        ))}
      </ol>
    </main>
  );
}
