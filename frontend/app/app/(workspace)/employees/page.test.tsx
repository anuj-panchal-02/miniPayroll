// @vitest-environment jsdom

import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import EmployeesPage from "./page";

const mocks = vi.hoisted(() => ({
  listEmployees: vi.fn(),
}));

vi.mock("@/lib/api", () => ({
  EmployeeStatus: { Active: 0, Inactive: 1, Draft: 2 },
  listEmployees: mocks.listEmployees,
}));

vi.mock("@/components/CompanyAdminShell", () => ({
  CompanyAdminShell: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

describe("EmployeesPage", () => {
  afterEach(cleanup);

  beforeEach(() => {
    mocks.listEmployees.mockReset();
  });

  it("shows an empty state", async () => {
    mocks.listEmployees.mockResolvedValue({
      employees: [],
      activeCount: 0,
      employeeLimit: 9,
    });

    render(<EmployeesPage />);

    expect(await screen.findByText(/no employees yet/i)).toBeTruthy();
    expect(screen.getByRole("link", { name: /add employee/i })).toBeTruthy();
  });

  it("shows a load error", async () => {
    mocks.listEmployees.mockRejectedValue(new Error("boom"));

    render(<EmployeesPage />);

    expect(await screen.findByRole("alert")).toHaveProperty("textContent", "boom");
  });

  it("keeps Add available at the active seat cap and shows Draft", async () => {
    mocks.listEmployees.mockResolvedValue({
      employees: [
        {
          id: "1",
          employeeCode: "EMP-01",
          fullName: "Ada",
          designation: "Engineer",
          department: null,
          status: 0,
          joiningDate: "2026-01-15",
          maskedAccountNumber: "****9012",
        },
        {
          id: "2",
          employeeCode: "EMP-02",
          fullName: "Grace",
          designation: "",
          department: null,
          status: 2,
          joiningDate: null,
          maskedAccountNumber: "****",
        },
      ],
      activeCount: 1,
      employeeLimit: 1,
    });

    render(<EmployeesPage />);

    expect(await screen.findByText(/ada/i)).toBeTruthy();
    expect(screen.getByRole("link", { name: /add employee/i })).toBeTruthy();
    expect(screen.getByText("Draft")).toBeTruthy();
  });
});
