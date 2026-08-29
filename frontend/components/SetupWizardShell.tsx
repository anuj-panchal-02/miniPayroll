"use client";

import { SuperadminShell } from "@/components/SuperadminShell";
import "@/app/app/setup/setup.css";

type SetupWizardShellProps = {
  children: React.ReactNode;
  currentStep: 1 | 2 | 3;
  title: string;
  description: string;
};

const STEPS = ["Company details", "Payroll settings", "Review"];

export function SetupWizardShell({
  children,
  currentStep,
  title,
  description,
}: SetupWizardShellProps) {
  return (
    <SuperadminShell
      role="Company Admin"
      homeHref="/app"
      requiredRole="CompanyAdmin"
      allowIncompleteSetup
    >
      <main className="setup-shell">
        <nav className="setup-progress" aria-label="Setup progress">
          <ol>
            {STEPS.map((step, index) => {
              const stepNumber = index + 1;
              const state =
                stepNumber < currentStep
                  ? "complete"
                  : stepNumber === currentStep
                    ? "current"
                    : "upcoming";
              return (
                <li key={step} data-state={state}>
                  <span
                    className="setup-progress__number"
                    aria-hidden="true"
                  >
                    {stepNumber}
                  </span>
                  <span>
                    <span
                      className="setup-progress__count"
                      aria-current={stepNumber === currentStep ? "step" : undefined}
                    >
                      Step {stepNumber} of {STEPS.length}
                    </span>
                    <span className="setup-progress__label">{step}</span>
                  </span>
                </li>
              );
            })}
          </ol>
        </nav>

        <section className="setup-content">
          <header className="setup-heading">
            <p className="setup-heading__eyebrow">Company setup</p>
            <h1>{title}</h1>
            <p>{description}</p>
          </header>
          {children}
        </section>
      </main>
    </SuperadminShell>
  );
}
