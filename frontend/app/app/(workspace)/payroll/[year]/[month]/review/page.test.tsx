// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import PayrollReviewPage from "./page";

const mocks = vi.hoisted(() => ({
  getPayrollPeriod: vi.fn(),
  calculatePayroll: vi.fn(),
  finalizePayroll: vi.fn(),
  downloadPayrollPayslip: vi.fn(),
  downloadAllPayrollPayslips: vi.fn(),
  updatePayrollPayment: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useParams: () => ({ year: "2026", month: "8" }),
}));

vi.mock("@/lib/api", () => ({
  PayrollRunStatus: { Draft: 0, Calculated: 1, Finalized: 2, Reversed: 3 },
  BonusType: { Festival: 0, Performance: 1, Attendance: 2, Incentive: 3, Other: 4 },
  OneTimeDeductionType: { AdvanceRecovery: 0, LoanInstallment: 1, Tds: 2, Other: 3 },
  SalaryPaymentStatus: { Unpaid: 0, Paid: 1 },
  SalaryPaymentMode: { Bank: 0, Upi: 1, Cash: 2 },
  getPayrollPeriod: mocks.getPayrollPeriod,
  calculatePayroll: mocks.calculatePayroll,
  finalizePayroll: mocks.finalizePayroll,
  downloadPayrollPayslip: mocks.downloadPayrollPayslip,
  downloadAllPayrollPayslips: mocks.downloadAllPayrollPayslips,
  updatePayrollPayment: mocks.updatePayrollPayment,
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
    finalizedAt: null,
  },
  employees: [],
  results: [
    {
      employeeId: "emp-1",
      employeeCode: "EMP-01",
      fullName: "Ada Lovelace",
      designation: "Engineer",
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
      paymentStatus: 0,
      paymentMode: null,
      paidOn: null,
      paymentReference: null,
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
    mocks.finalizePayroll.mockReset();
    mocks.downloadPayrollPayslip.mockReset();
    mocks.downloadAllPayrollPayslips.mockReset();
    mocks.updatePayrollPayment.mockReset();
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
      run: { ...calculated.run, status: 2, finalizedAt: "2026-09-01T00:00:00Z" },
    });

    render(<PayrollReviewPage />);

    expect(await screen.findByRole("button", { name: /recalculate/i })).toHaveProperty(
      "disabled",
      true,
    );
    expect(screen.getByText(/figures are locked/i)).toBeTruthy();
    expect(screen.queryByRole("button", { name: /^finalize$/i })).toBeNull();
  });

  it("confirms finalize before calling the API", async () => {
    mocks.getPayrollPeriod.mockResolvedValue(calculated);
    mocks.finalizePayroll.mockResolvedValue({ runStatus: 2 });

    render(<PayrollReviewPage />);

    fireEvent.click(await screen.findByRole("button", { name: /^finalize$/i }));
    expect(screen.getByRole("alertdialog").textContent).toMatch(/amounts cannot be changed/i);
    const confirm = screen.getAllByRole("button", { name: /^finalize$/i }).at(-1);
    fireEvent.click(confirm!);
    await waitFor(() => expect(mocks.finalizePayroll).toHaveBeenCalledWith("run-1"));
  });

  it("downloads payslips and marks paid on a finalized run", async () => {
    mocks.getPayrollPeriod.mockResolvedValue({
      ...calculated,
      run: { ...calculated.run, status: 2, finalizedAt: "2026-09-01T00:00:00Z" },
    });
    mocks.updatePayrollPayment.mockResolvedValue({
      ...calculated,
      run: { ...calculated.run, status: 2, finalizedAt: "2026-09-01T00:00:00Z" },
      results: [
        {
          ...calculated.results[0],
          paymentStatus: 1,
          paymentMode: 0,
          paidOn: "2026-09-01",
          paymentReference: "TXN-1",
        },
      ],
    });

    render(<PayrollReviewPage />);

    fireEvent.click(await screen.findByRole("button", { name: /download all/i }));
    await waitFor(() => expect(mocks.downloadAllPayrollPayslips).toHaveBeenCalledWith("run-1"));
    fireEvent.click(screen.getByRole("button", { name: /download payslip/i }));
    await waitFor(() =>
      expect(mocks.downloadPayrollPayslip).toHaveBeenCalledWith("run-1", "emp-1"),
    );
    fireEvent.click(screen.getByRole("button", { name: /mark paid/i }));
    await waitFor(() => expect(mocks.updatePayrollPayment).toHaveBeenCalled());
  });
});
