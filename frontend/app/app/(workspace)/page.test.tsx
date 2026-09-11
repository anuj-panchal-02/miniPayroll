// @vitest-environment jsdom

import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import CompanyDashboardPage from "./page";

const mocks = vi.hoisted(() => ({
  listEmployees: vi.fn(),
  getWorkspaceBilling: vi.fn(),
}));

vi.mock("@/lib/api", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/lib/api")>();
  return {
    ...actual,
    listEmployees: mocks.listEmployees,
    getWorkspaceBilling: mocks.getWorkspaceBilling,
  };
});

describe("CompanyDashboardPage", () => {
  afterEach(cleanup);

  beforeEach(() => {
    mocks.listEmployees.mockReset().mockResolvedValue({
      employees: [],
      activeCount: 2,
      employeeLimit: 9,
    });
    mocks.getWorkspaceBilling.mockReset();
  });

  it("shows the overdue banner when a period is unpaid past due", async () => {
    mocks.getWorkspaceBilling.mockResolvedValue({
      planName: "Basic",
      pricePerEmployee: 49,
      gracePeriodDays: 7,
      periods: [
        {
          billingPeriod: "2026-08",
          year: 2026,
          month: 8,
          billableEmployees: 2,
          billableSource: 0,
          pricePerEmployee: 49,
          amountDue: 98,
          prorated: false,
          isEstimated: false,
          dueDate: "2026-08-31T23:59:59+00:00",
          isOverdue: true,
          isPastGrace: false,
          paidAmount: 0,
          remaining: 98,
          payments: [],
        },
      ],
    });

    render(<CompanyDashboardPage />);

    expect(await screen.findByText("2 of 9")).toBeTruthy();
    expect(await screen.findByText("2 × ₹49 = ₹98")).toBeTruthy();
    expect(
      screen.getByText(/Payment is overdue. You can keep running payroll during the grace period/),
    ).toBeTruthy();
    const history = screen.getByRole("link", { name: "History" });
    expect(history.getAttribute("href")).toBe("/app/payroll/history");
    expect(history.closest("header")?.className).toContain("sa-head--with-back");
    const snapshot = screen.getByRole("region", { name: /snapshot/i });
    expect(snapshot.textContent).toMatch(/Headcount/);
    expect(snapshot.textContent).toMatch(/Period/);
    expect(snapshot.textContent).toMatch(/Plan/);
    expect(screen.getByRole("link", { name: "Process payroll" }).getAttribute("href")).toBe(
      "/app/payroll",
    );
    expect(screen.getByRole("link", { name: "Add employee" }).getAttribute("href")).toBe(
      "/app/employees/new",
    );
  });

  it("shows the open calendar month when later finalized periods exist", async () => {
    mocks.getWorkspaceBilling.mockResolvedValue({
      planName: "Basic",
      pricePerEmployee: 49,
      gracePeriodDays: 7,
      periods: [
        {
          billingPeriod: "2026-08",
          year: 2026,
          month: 8,
          billableEmployees: 2,
          billableSource: 0,
          pricePerEmployee: 49,
          amountDue: 98,
          prorated: false,
          isEstimated: false,
          dueDate: "2026-08-31T23:59:59+00:00",
          isOverdue: true,
          isPastGrace: false,
          paidAmount: 0,
          remaining: 98,
          payments: [],
        },
        {
          billingPeriod: "2026-09",
          year: 2026,
          month: 9,
          billableEmployees: 1,
          billableSource: 0,
          pricePerEmployee: 49,
          amountDue: 49,
          prorated: false,
          isEstimated: true,
          dueDate: "2026-09-30T23:59:59+00:00",
          isOverdue: false,
          isPastGrace: false,
          paidAmount: 0,
          remaining: 49,
          payments: [],
        },
        {
          billingPeriod: "2026-12",
          year: 2026,
          month: 12,
          billableEmployees: 2,
          billableSource: 0,
          pricePerEmployee: 49,
          amountDue: 98,
          prorated: false,
          isEstimated: false,
          dueDate: "2026-12-31T23:59:59+00:00",
          isOverdue: false,
          isPastGrace: false,
          paidAmount: 0,
          remaining: 98,
          payments: [],
        },
      ],
    });

    render(<CompanyDashboardPage />);

    expect(await screen.findByText(/1 × ₹49 = ₹49/)).toBeTruthy();
    expect(screen.getAllByText("September 2026").length).toBeGreaterThan(0);
    expect(screen.getByText(/1 × ₹49 = ₹49/)).toBeTruthy();
    expect(screen.queryByText("December 2026")).toBeNull();
    expect(screen.getByRole("region", { name: "Subscription" })).toBeTruthy();
    expect(screen.getByText("Paid")).toBeTruthy();
    expect(screen.getByText("Remaining")).toBeTruthy();
  });
});
