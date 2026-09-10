// @vitest-environment jsdom

import { fireEvent, render, screen, waitFor, cleanup } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import CompanyDetailsPage from "./page";

const mocks = vi.hoisted(() => ({
  replace: vi.fn(),
  push: vi.fn(),
  getToken: vi.fn(),
  getCompany: vi.fn(),
  getPlatformLimits: vi.fn(),
  createCompanyAdmin: vi.fn(),
  activateCompany: vi.fn(),
  updateCompanyLimit: vi.fn(),
  setToken: vi.fn(),
  listCompanyPayrollRuns: vi.fn(),
  reversePayrollRun: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useParams: () => ({ id: "co-1" }),
  useRouter: () => ({ replace: mocks.replace, push: mocks.push }),
  usePathname: () => "/superadmin/companies/co-1",
}));

vi.mock("@/lib/api", () => ({
  getToken: mocks.getToken,
  getCompany: mocks.getCompany,
  getPlatformLimits: mocks.getPlatformLimits,
  createCompanyAdmin: mocks.createCompanyAdmin,
  activateCompany: mocks.activateCompany,
  updateCompanyLimit: mocks.updateCompanyLimit,
  setToken: mocks.setToken,
  listCompanyPayrollRuns: mocks.listCompanyPayrollRuns,
  reversePayrollRun: mocks.reversePayrollRun,
  PayrollRunStatus: { Draft: 0, Calculated: 1, Finalized: 2, Reversed: 3 },
  BonusType: { Festival: 0, Performance: 1, Attendance: 2, Incentive: 3, Other: 4 },
  OneTimeDeductionType: { AdvanceRecovery: 0, LoanInstallment: 1, Tds: 2, Other: 3 },
}));

vi.mock("@/components/SuperadminShell", () => ({
  SuperadminShell: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

describe("CompanyDetailsPage", () => {
  afterEach(cleanup);

  beforeEach(() => {
    mocks.replace.mockReset();
    mocks.push.mockReset();
    mocks.getToken.mockReset();
    mocks.getCompany.mockReset();
    mocks.getPlatformLimits.mockReset();
    mocks.createCompanyAdmin.mockReset();
    mocks.updateCompanyLimit.mockReset();
    mocks.listCompanyPayrollRuns.mockReset();
    mocks.reversePayrollRun.mockReset();
    mocks.getToken.mockReturnValue("token");
    mocks.listCompanyPayrollRuns.mockResolvedValue([]);
    mocks.getPlatformLimits.mockResolvedValue({
      minEmployeeLimit: 1,
      hardEmployeeCap: 50,
      defaultEmployeeLimit: 50,
      defaultPlanName: "Basic",
      currencyCode: "INR",
    });
    mocks.getCompany.mockResolvedValue({
      id: "co-1",
      name: "ABC Traders",
      contactEmail: "owner@abctraders.example",
      contactPhone: null,
      status: "Pending",
      employeeLimit: 50,
      planName: "Basic",
      isSetupComplete: false,
      activatedAt: null,
      hasAdmin: false,
      adminEmail: null,
    });
    mocks.createCompanyAdmin.mockResolvedValue({
      userId: "user-1",
      email: "owner@abctraders.example",
    });
    mocks.updateCompanyLimit.mockResolvedValue({
      id: "co-1",
      name: "ABC Traders",
      contactEmail: "owner@abctraders.example",
      contactPhone: null,
      status: "Pending",
      employeeLimit: 20,
      planName: "Basic",
      isSetupComplete: false,
      activatedAt: null,
      hasAdmin: false,
      adminEmail: null,
    });
  });

  it("sends the Superadmin-typed password and does not display it", async () => {
    render(<CompanyDetailsPage />);

    const password = await screen.findByLabelText("Temporary password");
    fireEvent.change(password, { target: { value: "Tmp_TestAdmin1!" } });
    fireEvent.click(screen.getByRole("button", { name: "Create admin" }));

    await waitFor(() => {
      expect(mocks.createCompanyAdmin).toHaveBeenCalledWith(
        "co-1",
        "owner@abctraders.example",
        "Tmp_TestAdmin1!",
      );
    });

    expect(
      await screen.findByText(/Company Admin created for owner@abctraders.example/i),
    ).toBeTruthy();
    expect(screen.queryByRole("heading", { name: "Temporary password" })).toBeNull();
    expect(screen.queryByText("Tmp_TestAdmin1!")).toBeNull();
  });

  it("saves a new employee limit", async () => {
    render(<CompanyDetailsPage />);
    const limit = await screen.findByLabelText("Employee limit");
    fireEvent.change(limit, { target: { value: "20" } });
    fireEvent.click(screen.getByRole("button", { name: "Save employee limit" }));

    await waitFor(() => {
      expect(mocks.updateCompanyLimit).toHaveBeenCalledWith("co-1", 20);
    });
    expect(await screen.findByText("Employee limit saved.")).toBeTruthy();
  });
});
