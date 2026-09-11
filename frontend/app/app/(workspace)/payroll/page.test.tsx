// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import PayrollPage from "./page";

const mocks = vi.hoisted(() => ({
  push: vi.fn(),
  getPayrollPeriod: vi.fn(),
  createPayrollRun: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: mocks.push }),
}));

vi.mock("@/lib/api", () => ({
  PayrollRunStatus: { Draft: 0, Calculated: 1, Finalized: 2, Reversed: 3 },
  BonusType: { Festival: 0, Performance: 1, Attendance: 2, Incentive: 3, Other: 4 },
  OneTimeDeductionType: { AdvanceRecovery: 0, LoanInstallment: 1, Tds: 2, Other: 3 },
  getPayrollPeriod: mocks.getPayrollPeriod,
  createPayrollRun: mocks.createPayrollRun,
}));

vi.mock("@/components/CompanyAdminShell", () => ({
  CompanyAdminShell: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

const employee = {
  employeeId: "emp-1",
  employeeCode: "EMP-01",
  fullName: "Ada Lovelace",
  status: 0,
  joiningDate: "2026-01-01",
  exitDate: null,
  hasStructure: true,
  overtimeRate: 150,
  attendance: null,
  overtime: [],
  bonuses: [],
  deductions: [],
};

describe("PayrollPage", () => {
  afterEach(cleanup);

  beforeEach(() => {
    mocks.push.mockReset();
    mocks.getPayrollPeriod.mockReset();
    mocks.createPayrollRun.mockReset();
  });

  it("shows start payroll when no run exists", async () => {
    mocks.getPayrollPeriod.mockResolvedValue({
      year: 2026,
      month: 9,
      workingDaysPerMonth: 26,
      run: null,
      employees: [employee],
      results: [],
      totals: null,
    });

    render(<PayrollPage />);

    expect(await screen.findByRole("button", { name: /start payroll/i })).toBeTruthy();
    expect(screen.getByLabelText("Month").getAttribute("aria-haspopup")).toBe("listbox");
    expect(screen.getByLabelText("Year").getAttribute("aria-haspopup")).toBe("listbox");
    expect(screen.queryByRole("combobox")).toBeNull();

    const history = screen.getByRole("link", { name: "History" });
    expect(history.getAttribute("href")).toBe("/app/payroll/history");
    expect(history.closest("header")?.className).toContain("sa-head--with-back");

    expect(screen.getByText("Status")).toBeTruthy();
    expect(screen.getByText("No run")).toBeTruthy();
    expect(screen.getByText("Employees")).toBeTruthy();
    expect(screen.getByText("Readiness")).toBeTruthy();
    expect(screen.getByText(/salary structures complete/i)).toBeTruthy();
  });

  it("starts a run and opens monthly inputs", async () => {
    mocks.getPayrollPeriod.mockResolvedValue({
      year: 2026,
      month: 9,
      workingDaysPerMonth: 26,
      run: null,
      employees: [employee],
      results: [],
      totals: null,
    });
    mocks.createPayrollRun.mockResolvedValue({ id: "run-1" });

    render(<PayrollPage />);
    fireEvent.click(await screen.findByRole("button", { name: /start payroll/i }));

    await waitFor(() => expect(mocks.createPayrollRun).toHaveBeenCalled());
    expect(mocks.push).toHaveBeenCalledWith(expect.stringMatching(/^\/app\/payroll\/\d+\/\d+$/));
  });

  it("links to inputs and review when a run exists", async () => {
    mocks.getPayrollPeriod.mockResolvedValue({
      year: 2026,
      month: 8,
      workingDaysPerMonth: 26,
      run: {
        id: "run-1",
        status: 0,
        dailyRateMethod: 0,
        createdAt: "2026-08-01T00:00:00Z",
        calculatedAt: null,
      },
      employees: [{ ...employee, hasStructure: false }],
      results: [],
      totals: null,
    });

    render(<PayrollPage />);

    expect(await screen.findByRole("link", { name: /monthly inputs/i })).toBeTruthy();
    expect(screen.getByRole("link", { name: /review/i })).toBeTruthy();
    expect(screen.getByText("Draft")).toBeTruthy();
    expect(screen.getByText(/1 missing salary structure/i)).toBeTruthy();
    expect(screen.getByText(/1 missing attendance/i)).toBeTruthy();
  });

  it("shows a load error", async () => {
    mocks.getPayrollPeriod.mockRejectedValue(new Error("boom"));
    render(<PayrollPage />);
    expect(await screen.findByRole("alert")).toHaveProperty("textContent", "boom");
  });
});
