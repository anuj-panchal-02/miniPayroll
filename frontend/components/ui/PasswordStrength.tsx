"use client";

import { PASSWORD_STRENGTH_MAX, type PasswordStrength as Strength } from "@/lib/validation";

type PasswordStrengthProps = {
  id: string;
  strength: Strength;
  /** Suppressed when the field's error slot already carries the same guidance. */
  showAdvice?: boolean;
};

export function PasswordStrength({ id, strength, showAdvice = true }: PasswordStrengthProps) {
  const percent = Math.round((strength.score / PASSWORD_STRENGTH_MAX) * 100);

  return (
    <div className="mp-field__meter" data-tone={strength.tone}>
      <span className="mp-field__meter-track" aria-hidden="true">
        <span className="mp-field__meter-fill" style={{ width: `${percent}%` }} />
      </span>
      <p id={id} className="mp-field__meter-label" role="status">
        {strength.label}
        {showAdvice && strength.advice ? ` ${strength.advice}` : null}
      </p>
    </div>
  );
}
