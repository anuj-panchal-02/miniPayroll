// @vitest-environment jsdom

import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { CompanySetupStep } from "@/lib/setup";
import ReviewSetupPage from "./page";

const mocks = vi.hoisted(() => ({
  replace: vi.fn(),
  getCompanySetup: vi.fn(),
  completeCompanySetup: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: mocks.replace }),
}));

vi.mock("@/lib/api", () => ({
  getCompanySetup: mocks.getCompanySetup,
  completeCompanySetup: mocks.completeCompanySetup,
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
  logoUrl: "http://localhost:5238/uploads/logo.png",
  payrollCycle: "Monthly",
  dailyRateMethod: "CalendarDays" as const,
  workingDaysPerMonth: 26,
  weeklyOffDays: ["Sunday"],
  setupStep: CompanySetupStep.Review,
  isSetupComplete: false,
};

describe("ReviewSetupPage", () => {
  afterEach(cleanup);

  beforeEach(() => {
    mocks.replace.mockReset();
    mocks.getCompanySetup.mockReset().mockResolvedValue(setup);
    mocks.completeCompanySetup
      .mockReset()
      .mockResolvedValue({ ...setup, isSetupComplete: true, setupStep: CompanySetupStep.Complete });
  });

  it("shows a read-only grouped summary with edit links", async () => {
    render(<ReviewSetupPage />);

    expect(await screen.findByText("Acme Ltd")).toBeTruthy();
    expect(screen.getByRole("link", { name: /edit company details/i })).toBeTruthy();
    expect(screen.getByRole("link", { name: /edit payroll settings/i })).toBeTruthy();
    expect(screen.getByAltText(/acme ltd logo/i).getAttribute("src")).toBe(
      "http://localhost:5238/uploads/logo.png",
    );
  });

  it("navigates only after completion succeeds", async () => {
    let resolveComplete!: (value: typeof setup) => void;
    mocks.completeCompanySetup.mockReturnValueOnce(
      new Promise((resolve) => {
        resolveComplete = resolve;
      }),
    );
    render(<ReviewSetupPage />);
    await screen.findByText("Acme Ltd");

    fireEvent.click(screen.getByRole("button", { name: /complete setup/i }));
    expect(mocks.replace).not.toHaveBeenCalled();

    resolveComplete(setup);
    await waitFor(() => expect(mocks.replace).toHaveBeenCalledWith("/app"));
  });

  it("keeps the user on review when backend validation fails", async () => {
    mocks.completeCompanySetup.mockRejectedValueOnce(
      new Error("Upload a company logo before completing setup."),
    );
    render(<ReviewSetupPage />);
    await screen.findByText("Acme Ltd");

    fireEvent.click(screen.getByRole("button", { name: /complete setup/i }));

    expect(
      await screen.findByText("Upload a company logo before completing setup."),
    ).toBeTruthy();
    expect(mocks.replace).not.toHaveBeenCalled();
  });

  it("does not navigate when completion resolves after unmount", async () => {
    let resolveComplete!: (value: typeof setup) => void;
    mocks.completeCompanySetup.mockReturnValueOnce(
      new Promise((resolve) => {
        resolveComplete = resolve;
      }),
    );
    const view = render(<ReviewSetupPage />);
    await screen.findByText("Acme Ltd");
    fireEvent.click(screen.getByRole("button", { name: /complete setup/i }));
    await waitFor(() => expect(mocks.completeCompanySetup).toHaveBeenCalledOnce());

    view.unmount();
    await act(async () => {
      resolveComplete(setup);
      await Promise.resolve();
    });

    expect(mocks.replace).not.toHaveBeenCalled();
  });
});
