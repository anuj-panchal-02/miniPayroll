// @vitest-environment jsdom

import { readFileSync } from "node:fs";
import { join } from "node:path";
import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { SetupWizardShell } from "./SetupWizardShell";

describe("SetupWizardShell", () => {
  it("exposes progress accessibly without wrapping the app shell", () => {
    render(
      <SetupWizardShell
        currentStep={2}
        title="Payroll settings"
        description="Choose payroll defaults."
      >
        <p>Form content</p>
      </SetupWizardShell>,
    );

    expect(screen.getByRole("navigation", { name: /setup progress/i })).toBeTruthy();
    expect(screen.queryByRole("navigation", { name: /company/i })).toBeNull();
    expect(screen.queryByText(/coming soon/i)).toBeNull();
    expect(screen.getByText("Step 2 of 3").getAttribute("aria-current")).toBe("step");
    expect(screen.getByRole("heading", { name: "Payroll settings" })).toBeTruthy();
  });

  it("keeps step labels in the accessibility tree on small screens", () => {
    const { container } = render(
      <SetupWizardShell
        currentStep={1}
        title="Company details"
        description="Tell us about the business."
      >
        <p>Form content</p>
      </SetupWizardShell>,
    );

    const labels = Array.from(
      container.querySelectorAll(".setup-progress__label"),
    );
    expect(labels.map((label) => label.textContent)).toEqual([
      "Company details",
      "Payroll settings",
      "Review",
    ]);
    labels.forEach((label) =>
      expect(label.closest("[aria-hidden='true']")).toBeNull(),
    );

    const css = readFileSync(
      join(process.cwd(), "components", "ui", "form-controls.css"),
      "utf8",
    );
    const smallScreens = css.slice(css.indexOf("@media (max-width: 39.99rem)"));

    expect(smallScreens).toMatch(/\.setup-progress__label/);
    expect(smallScreens).not.toMatch(/display:\s*none/);
    expect(smallScreens).toMatch(/clip-path:\s*inset\(50%\)/);
  });
});
