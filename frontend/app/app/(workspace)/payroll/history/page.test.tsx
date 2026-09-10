// @vitest-environment jsdom

import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import PayrollHistoryPage from "./page";

const mocks = vi.hoisted(() => ({
  listPayrollRuns: vi.fn(),
}));

vi.mock("@/lib/api", () => ({
  PayrollRunStatus: { Draft: 0, Calculated: 1, Finalized: 2, Reversed: 3 },
  BonusType: { Festival: 0, Performance: 1, Attendance: 2, Incentive: 3, Other: 4 },
  OneTimeDeductionType: { AdvanceRecovery: 0, LoanInstallment: 1, Tds: 2, Other: 3 },
  listPayrollRuns: mocks.listPayrollRuns,
}));

vi.mock("@/components/CompanyAdminShell", () => ({
  CompanyAdminShell: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

describe("PayrollHistoryPage", () => {
  afterEach(cleanup);

  beforeEach(() => {
    mocks.listPayrollRuns.mockReset();
  });

  it("lists run totals and status", async () => {
    mocks.listPayrollRuns.mockResolvedValue([
      {
        id: "run-1",
        year: 2026,
        month: 8,
        status: 2,
        employeeCount: 1,
        grossEarnings: 28000,
        totalDeductions: 0,
        netSalary: 28000,
        createdAt: "2026-08-01T00:00:00Z",
        calculatedAt: "2026-08-31T00:00:00Z",
        finalizedAt: "2026-09-01T00:00:00Z",
      },
    ]);

    render(<PayrollHistoryPage />);

    expect(await screen.findByText("August 2026")).toBeTruthy();
    expect(screen.getByText("Finalized")).toBeTruthy();
    expect(screen.getByText("₹28,000")).toBeTruthy();
    expect(screen.getByText(/1 closed · 1 total/i)).toBeTruthy();
  });
});
