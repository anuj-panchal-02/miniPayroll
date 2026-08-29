// @vitest-environment jsdom

import { render, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { CompanySetupStep } from "@/lib/setup";
import SetupPage from "./page";

const mocks = vi.hoisted(() => ({
  replace: vi.fn(),
  getCompanySetup: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: mocks.replace }),
}));

vi.mock("@/lib/api", () => ({
  getCompanySetup: mocks.getCompanySetup,
}));

vi.mock("@/components/SetupWizardShell", () => ({
  SetupWizardShell: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

describe("SetupPage", () => {
  beforeEach(() => {
    mocks.replace.mockReset();
    mocks.getCompanySetup.mockReset();
  });

  it("loads setup and routes to the persisted step", async () => {
    mocks.getCompanySetup.mockResolvedValue({
      setupStep: CompanySetupStep.PayrollSettings,
      isSetupComplete: false,
    });

    render(<SetupPage />);

    await waitFor(() => {
      expect(mocks.replace).toHaveBeenCalledWith("/app/setup/payroll");
    });
  });

  it("routes completed companies to the application", async () => {
    mocks.getCompanySetup.mockResolvedValue({
      setupStep: CompanySetupStep.Complete,
      isSetupComplete: true,
    });

    render(<SetupPage />);

    await waitFor(() => expect(mocks.replace).toHaveBeenCalledWith("/app"));
  });
});
