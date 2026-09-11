// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { CompanySetupStep } from "@/lib/setup";
import CompanySettingsPage from "./page";

const mocks = vi.hoisted(() => ({
  getCompanySetup: vi.fn(),
  updateCompletedPayrollSettings: vi.fn(),
}));

vi.mock("@/lib/api", () => ({
  DailyRateMethod: { CalendarDays: 0, FixedThirty: 1 },
  getCompanySetup: mocks.getCompanySetup,
  updateCompletedPayrollSettings: mocks.updateCompletedPayrollSettings,
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
  pfApplicable: true,
  pfUseWageCeiling: true,
  esiApplicable: true,
  pfEstablishmentCode: null,
  esiCode: null,
  setupStep: CompanySetupStep.Complete,
  isSetupComplete: true,
};

describe("CompanySettingsPage", () => {
  afterEach(cleanup);

  beforeEach(() => {
    mocks.getCompanySetup.mockReset().mockResolvedValue(setup);
    mocks.updateCompletedPayrollSettings.mockReset().mockResolvedValue(setup);
  });

  it("loads daily-rate and statutory policy", async () => {
    render(<CompanySettingsPage />);

    expect(await screen.findByDisplayValue("26")).toBeTruthy();
    expect(screen.getByRole("group", { name: /statutory deductions/i })).toBeTruthy();
    expect((screen.getByRole("checkbox", { name: /provident fund/i }) as HTMLInputElement).checked).toBe(
      true,
    );
    expect(screen.getByText(/professional tax and lwf use the company state \(maharashtra\)/i)).toBeTruthy();
  });

  it("saves completed payroll settings including statutory policy", async () => {
    render(<CompanySettingsPage />);
    await screen.findByDisplayValue("26");
    fireEvent.click(screen.getByRole("checkbox", { name: /esi/i }));
    fireEvent.click(screen.getByRole("button", { name: /save settings/i }));

    await waitFor(() => {
      expect(mocks.updateCompletedPayrollSettings).toHaveBeenCalledWith({
        dailyRateMethod: 0,
        workingDaysPerMonth: 26,
        weeklyOffDays: ["Sunday"],
        pfApplicable: true,
        pfUseWageCeiling: true,
        esiApplicable: false,
        pfEstablishmentCode: null,
        esiCode: null,
      });
    });
  });
});
