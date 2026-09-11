"use client";

import { Stepper } from "@/components/ui/Stepper";
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
    <main className="setup-shell">
      <Stepper
        className="setup-progress"
        label="Setup progress"
        currentStep={currentStep}
        steps={STEPS}
      />

      <section className="setup-content">
        <header className="setup-heading">
          <p className="setup-heading__eyebrow">Company setup</p>
          <h1>{title}</h1>
          <p>{description}</p>
        </header>
        {children}
      </section>
    </main>
  );
}
