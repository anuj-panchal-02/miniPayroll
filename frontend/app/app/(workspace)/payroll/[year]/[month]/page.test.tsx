// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import MonthlyInputsPage from "./page";

const mocks = vi.hoisted(() => ({
  getPayrollPeriod: vi.fn(),
  savePayrollInputs: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useParams: () => ({ year: "2026", month: "8" }),
}));

vi.mock("@/lib/api", () => ({
  PayrollRunStatus: { Draft: 0, Calculated: 1, Finalized: 2, Reversed: 3 },
  BonusType: { Festival: 0, Performance: 1, Attendance: 2, Incentive: 3, Other: 4 },
  OneTimeDeductionType: { AdvanceRecovery: 0, LoanInstallment: 1, Tds: 2, Other: 3 },
  getPayrollPeriod: mocks.getPayrollPeriod,
  savePayrollInputs: mocks.savePayrollInputs,
}));

vi.mock("@/components/CompanyAdminShell", () => ({
  CompanyAdminShell: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

const period = {
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
  employees: [
    {
      employeeId: "emp-1",
      employeeCode: "EMP-01",
      fullName: "Ada Lovelace",
      status: 0,
      joiningDate: "2026-01-01",
      exitDate: null,
      hasStructure: true,
      overtimeRate: 150,
      attendance: {
        employeeId: "emp-1",
        workingDays: 26,
        present: 26,
        paidLeave: 0,
        unpaidLeave: 0,
      },
      overtime: [],
      bonuses: [],
      deductions: [],
    },
  ],
  results: [],
  totals: null,
};

describe("MonthlyInputsPage", () => {
  afterEach(cleanup);

  beforeEach(() => {
    mocks.getPayrollPeriod.mockReset();
    mocks.savePayrollInputs.mockReset();
  });

  it("renders the roster and saves attendance", async () => {
    mocks.getPayrollPeriod.mockResolvedValue(period);
    mocks.savePayrollInputs.mockResolvedValue(period);

    render(<MonthlyInputsPage />);

    expect(await screen.findByText("Ada Lovelace")).toBeTruthy();
    fireEvent.change(screen.getByLabelText(/unpaid leave for ada lovelace/i), {
      target: { value: "1" },
    });
    fireEvent.change(screen.getByLabelText(/present days for ada lovelace/i), {
      target: { value: "25" },
    });
    fireEvent.click(screen.getByRole("button", { name: /save inputs/i }));

    await waitFor(() => expect(mocks.savePayrollInputs).toHaveBeenCalled());
    expect(mocks.savePayrollInputs).toHaveBeenCalledWith("run-1", {
      attendance: [
        {
          employeeId: "emp-1",
          workingDays: 26,
          present: 25,
          paidLeave: 0,
          unpaidLeave: 1,
        },
      ],
      overtime: [],
      bonuses: [],
      deductions: [],
    });
  });

  it("warns when attendance identity is violated", async () => {
    mocks.getPayrollPeriod.mockResolvedValue(period);
    render(<MonthlyInputsPage />);

    fireEvent.change(await screen.findByLabelText(/present days for ada lovelace/i), {
      target: { value: "20" },
    });

    expect(screen.getByText(/present \+ paid \+ unpaid must equal working days/i)).toBeTruthy();
  });

  it("shows a locked message for finalized runs", async () => {
    mocks.getPayrollPeriod.mockResolvedValue({
      ...period,
      run: { ...period.run, status: 2 },
    });

    render(<MonthlyInputsPage />);

    expect(await screen.findByText(/finalized or reversed and cannot be changed/i)).toBeTruthy();
    expect(screen.getByRole("button", { name: /save inputs/i })).toHaveProperty("disabled", true);
  });

  it("asks the admin to start payroll when no run exists", async () => {
    mocks.getPayrollPeriod.mockResolvedValue({ ...period, run: null });
    render(<MonthlyInputsPage />);
    expect(await screen.findByText(/no payroll run for this month/i)).toBeTruthy();
    expect(screen.getByRole("link", { name: /start payroll/i })).toBeTruthy();
  });
});
