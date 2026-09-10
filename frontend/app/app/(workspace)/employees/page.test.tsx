// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
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
    expect(screen.getAllByText("Draft").length).toBeGreaterThan(0);
  });

  it("filters the list and announces the result count", async () => {
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
      employeeLimit: 9,
    });

    render(<EmployeesPage />);
    expect(await screen.findByText(/ada/i)).toBeTruthy();
    fireEvent.change(screen.getByLabelText("Search employees"), {
      target: { value: "grace" },
    });
    expect(screen.getByText(/grace/i)).toBeTruthy();
    expect(screen.queryByText(/ada/i)).toBeNull();
    expect(screen.getByRole("status").textContent).toContain("1 employee");
    fireEvent.click(screen.getByRole("button", { name: "Clear" }));
    expect(screen.getByText(/ada/i)).toBeTruthy();
  });

  it("pages the employee list", async () => {
    mocks.listEmployees.mockResolvedValue({
      employees: Array.from({ length: 11 }, (_, index) => ({
        id: String(index + 1),
        employeeCode: `EMP-${String(index + 1).padStart(2, "0")}`,
        fullName: `Person ${index + 1}`,
        designation: "Engineer",
        department: null,
        status: 0,
        joiningDate: "2026-01-15",
        maskedAccountNumber: "****9012",
      })),
      activeCount: 11,
      employeeLimit: 20,
    });

    render(<EmployeesPage />);
    expect(await screen.findByText(/person 1 · emp-01/i)).toBeTruthy();
    expect(screen.getByText(/person 10 ·/i)).toBeTruthy();
    expect(screen.queryByText(/person 11 ·/i)).toBeNull();
    expect(screen.getByText("1–10 of 11")).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Next" }));
    expect(screen.getByText(/person 11 ·/i)).toBeTruthy();
    expect(screen.queryByText(/person 1 · emp-01/i)).toBeNull();
  });
});
