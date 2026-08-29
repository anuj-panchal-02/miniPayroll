// @vitest-environment jsdom

import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { CompanySetupStep } from "@/lib/setup";
import PayrollSetupPage from "./page";

const mocks = vi.hoisted(() => ({
  push: vi.fn(),
  getCompanySetup: vi.fn(),
  updatePayrollSettings: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: mocks.push }),
}));

vi.mock("@/lib/api", () => ({
  DailyRateMethod: { CalendarDays: 0, FixedThirty: 1 },
  getCompanySetup: mocks.getCompanySetup,
  updatePayrollSettings: mocks.updatePayrollSettings,
}));

vi.mock("@/components/SetupWizardShell", () => ({
  SetupWizardShell: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

const setup = {
  name: "Acme Ltd",
  contactEmail: "payroll@acme.test",
  contactPhone: "9876543210",
  addressLine1: "1 Main Street",
  addressLine2: null,
  city: "Pune",
  state: "Maharashtra",
  postalCode: "411001",
  logoUrl: "/logo.png",
  payrollCycle: "Monthly",
  dailyRateMethod: "CalendarDays" as const,
  workingDaysPerMonth: 26,
  weeklyOffDays: ["Sunday"],
  setupStep: CompanySetupStep.PayrollSettings,
  isSetupComplete: false,
};

describe("PayrollSetupPage", () => {
  afterEach(cleanup);

  beforeEach(() => {
    mocks.push.mockReset();
    mocks.getCompanySetup.mockReset().mockResolvedValue(setup);
    mocks.updatePayrollSettings
      .mockReset()
      .mockResolvedValue({ ...setup, setupStep: CompanySetupStep.Review });
  });

  it("loads monthly defaults and explains both daily-rate formulas", async () => {
    render(<PayrollSetupPage />);

    expect(await screen.findByDisplayValue("26")).toBeTruthy();
    expect(screen.getByText("Monthly")).toBeTruthy();
    expect((screen.getByLabelText("Sunday") as HTMLInputElement).checked).toBe(true);
    expect(screen.getByText(/monthly salary.*calendar days/i)).toBeTruthy();
    expect(screen.getByText(/monthly salary.*30/i)).toBeTruthy();
  });

  it("blocks invalid settings without making a request", async () => {
    render(<PayrollSetupPage />);
    const workingDays = await screen.findByLabelText(/working days per month/i);
    fireEvent.change(workingDays, { target: { value: "32" } });

    fireEvent.click(screen.getByRole("button", { name: /save and continue/i }));

    expect(await screen.findByRole("alert")).toBeTruthy();
    expect(mocks.updatePayrollSettings).not.toHaveBeenCalled();
  });

  it("saves settings and navigates to review", async () => {
    render(<PayrollSetupPage />);
    await screen.findByDisplayValue("26");

    fireEvent.click(screen.getByRole("button", { name: /save and continue/i }));

    await waitFor(() => {
      expect(mocks.updatePayrollSettings).toHaveBeenCalledWith({
        dailyRateMethod: 0,
        workingDaysPerMonth: 26,
        weeklyOffDays: ["Sunday"],
      });
      expect(mocks.push).toHaveBeenCalledWith("/app/setup/review");
    });
  });

  it("associates weekly-off errors and focuses the first checkbox", async () => {
    render(<PayrollSetupPage />);
    await screen.findByDisplayValue("26");
    fireEvent.click(screen.getByLabelText("Sunday"));

    fireEvent.click(screen.getByRole("button", { name: /save and continue/i }));

    const error = await screen.findByText("Select at least one weekly off day.");
    const group = screen.getByRole("group", { name: /weekly off days/i });
    const firstCheckbox = screen.getByLabelText("Monday");
    expect(group.getAttribute("aria-invalid")).toBe("true");
    expect(group.getAttribute("aria-describedby")).toContain(error.id);
    expect(firstCheckbox.getAttribute("aria-invalid")).toBe("true");
    expect(firstCheckbox.getAttribute("aria-describedby")).toContain(error.id);
    expect(document.activeElement).toBe(firstCheckbox);
    expect(mocks.updatePayrollSettings).not.toHaveBeenCalled();
  });

  it("does not navigate when an in-flight save resolves after unmount", async () => {
    let resolveSave!: (value: typeof setup) => void;
    mocks.updatePayrollSettings.mockReturnValueOnce(
      new Promise((resolve) => {
        resolveSave = resolve;
      }),
    );
    const view = render(<PayrollSetupPage />);
    await screen.findByDisplayValue("26");
    fireEvent.click(screen.getByRole("button", { name: /save and continue/i }));
    await waitFor(() => expect(mocks.updatePayrollSettings).toHaveBeenCalledOnce());

    view.unmount();
    await act(async () => {
      resolveSave(setup);
      await Promise.resolve();
    });

    expect(mocks.push).not.toHaveBeenCalled();
  });
});
