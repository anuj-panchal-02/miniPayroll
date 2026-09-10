type StepperProps = {
  label: string;
  currentStep: number;
  steps: readonly string[];
  className?: string;
};

export function Stepper({ label, currentStep, steps, className }: StepperProps) {
  return (
    <nav className={["mp-stepper", className].filter(Boolean).join(" ")} aria-label={label}>
      <ol>
        {steps.map((step, index) => {
          const stepNumber = index + 1;
          const state =
            stepNumber < currentStep
              ? "complete"
              : stepNumber === currentStep
                ? "current"
                : "upcoming";
          return (
            <li key={step} data-state={state}>
              <span className="mp-stepper__number setup-progress__number sa-progress__number" aria-hidden="true">
                {stepNumber}
              </span>
              <span>
                <span
                  className="mp-stepper__count setup-progress__count sa-progress__count"
                  aria-current={stepNumber === currentStep ? "step" : undefined}
                >
                  Step {stepNumber} of {steps.length}
                </span>
                <span className="mp-stepper__label setup-progress__label sa-progress__label">{step}</span>
              </span>
            </li>
          );
        })}
      </ol>
    </nav>
  );
}
