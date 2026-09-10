// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import PayrollReviewPage from "./page";

const mocks = vi.hoisted(() => ({
  getPayrollPeriod: vi.fn(),
  calculatePayroll: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useParams: () => ({ year: "2026", month: "8" }),
}));

vi.mock("@/lib/api", () => ({
  PayrollRunStatus: { Draft: 0, Calculated: 1, Finalized: 2, Reversed: 3 },
  BonusType: { Festival: 0, Performance: 1, Attendance: 2, Incentive: 3, Other: 4 },
  OneTimeDeductionType: { AdvanceRecovery: 0, LoanInstallment: 1, Tds: 2, Other: 3 },
  getPayrollPeriod: mocks.getPayrollPeriod,
  calculatePayroll: mocks.calculatePayroll,
}));

vi.mock("@/components/CompanyAdminShell", () => ({
  CompanyAdminShell: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

const calculated = {
  year: 2026,
  month: 8,
  workingDaysPerMonth: 26,
  run: {
    id: "run-1",
    status: 1,
    dailyRateMethod: 0,
    createdAt: "2026-08-01T00:00:00Z",
    calculatedAt: "2026-08-31T00:00:00Z",
  },
  employees: [],
  results: [
    {
      employeeId: "emp-1",
      employeeCode: "EMP-01",
      fullName: "Ada Lovelace",
      daysEmployed: 31,
      dailyRate: 903.225806,
      grossEarnings: 28000,
      totalDeductions: 0,
      netSalary: 28000,
      earnings: [
        { name: "Basic Salary", kind: 0, amount: 20000, sortOrder: 0 },
        { name: "HRA", kind: 0, amount: 8000, sortOrder: 1 },
      ],
      deductions: [],
      warnings: [],
      errors: [],
    },
  ],
  totals: {
    grossEarnings: 28000,
    totalDeductions: 0,
    netSalary: 28000,
    employeeCount: 1,
    warningCount: 0,
    errorCount: 0,
  },
};

describe("PayrollReviewPage", () => {
  afterEach(cleanup);

  beforeEach(() => {
    mocks.getPayrollPeriod.mockReset();
    mocks.calculatePayroll.mockReset();
  });

  it("renders totals and recalculates", async () => {
    mocks.getPayrollPeriod.mockResolvedValue(calculated);
    mocks.calculatePayroll.mockResolvedValue({ runStatus: 1 });

    render(<PayrollReviewPage />);

    expect(await screen.findByText("Ada Lovelace")).toBeTruthy();
    expect(screen.getByText(/1 paid · 0 warnings · 0 errors/i)).toBeTruthy();
    expect(screen.getAllByText("₹28,000").length).toBeGreaterThan(0);

    fireEvent.click(screen.getByRole("button", { name: /recalculate/i }));
    await waitFor(() => expect(mocks.calculatePayroll).toHaveBeenCalledWith(2026, 8));
    expect(mocks.getPayrollPeriod.mock.calls.length).toBeGreaterThan(1);
  });

  it("lists blocking errors first and keeps draft messaging", async () => {
    mocks.getPayrollPeriod.mockResolvedValue({
      ...calculated,
      run: { ...calculated.run, status: 0, calculatedAt: null },
      results: [
        {
          ...calculated.results[0],
          employeeId: "emp-2",
          employeeCode: "EMP-02",
          fullName: "Grace Hopper",
          netSalary: 0,
          errors: ["Attendance not entered for this employee."],
        },
        calculated.results[0],
      ],
      totals: { ...calculated.totals, employeeCount: 2, errorCount: 1 },
    });

    render(<PayrollReviewPage />);

    expect(await screen.findByText(/draft/i)).toBeTruthy();
    const names = screen.getAllByText(/lovelace|hopper/i).map((node) => node.textContent);
    expect(names[0]).toMatch(/grace hopper/i);
    expect(screen.getByText(/attendance not entered/i)).toBeTruthy();
    expect(screen.queryByText(/inputs changed after the last calculation/i)).toBeNull();
  });

  it("shows a stale banner after inputs change a successful calculation", async () => {
    mocks.getPayrollPeriod.mockResolvedValue({
      ...calculated,
      run: { ...calculated.run, status: 0, calculatedAt: null },
    });

    render(<PayrollReviewPage />);

    expect(
      await screen.findByText(/inputs changed after the last calculation/i),
    ).toBeTruthy();
  });

  it("disables calculate on a finalized run", async () => {
    mocks.getPayrollPeriod.mockResolvedValue({
      ...calculated,
      run: { ...calculated.run, status: 2 },
    });

    render(<PayrollReviewPage />);

    expect(await screen.findByRole("button", { name: /recalculate/i })).toHaveProperty(
      "disabled",
      true,
    );
    expect(screen.getByText(/cannot be recalculated/i)).toBeTruthy();
  });
});
