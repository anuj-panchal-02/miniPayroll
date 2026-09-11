// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { ToastProvider } from "@/components/Toast";
import EditEmployeePage from "./page";

const mocks = vi.hoisted(() => ({
  push: vi.fn(),
  getEmployee: vi.fn(),
  updateEmployee: vi.fn(),
  listSalaryStructures: vi.fn(),
  listPlatformStates: vi.fn(),
  listPlatformCities: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: mocks.push }),
  useParams: () => ({ id: "abc" }),
}));

vi.mock("@/lib/api", () => ({
  EmployeeStatus: { Active: 0, Inactive: 1, Draft: 2 },
  Gender: { Male: 0, Female: 1 },
  SalaryComponentType: { Earning: 0, Deduction: 1 },
  SalaryComponentValueType: { FixedAmount: 0, PercentageOfBasic: 1 },
  BonusType: { Festival: 0, Performance: 1, Attendance: 2, Incentive: 3, Other: 4 },
  OneTimeDeductionType: { AdvanceRecovery: 0, LoanInstallment: 1, Tds: 2, Other: 3 },
  getEmployee: mocks.getEmployee,
  updateEmployee: mocks.updateEmployee,
  listSalaryStructures: mocks.listSalaryStructures,
  listPlatformStates: mocks.listPlatformStates,
  listPlatformCities: mocks.listPlatformCities,
}));

const employee = {
  id: "abc",
  employeeCode: "EMP-01",
  fullName: "Ada Lovelace",
  dateOfBirth: null,
  phone: "9876543210",
  email: "ada@example.com",
  addressLine1: "Main Road",
  addressLine2: null,
  city: "Pune",
  state: "Maharashtra",
  postalCode: "411001",
  designation: "Engineer",
  department: null,
  employmentType: 0,
  joiningDate: "2026-01-15",
  exitDate: null,
  status: 0,
  draftStep: null,
  bankName: "HDFC Bank",
  bankAccountNumber: "123456789012",
  maskedAccountNumber: "****9012",
  ifsc: "HDFC0001234",
  upiId: null,
  overtimeRate: null,
  gender: 0,
  pfCovered: true,
  esiCovered: true,
  uan: null,
  pfNumber: null,
  esiNumber: null,
};

const draftEmployee = {
  ...employee,
  status: 2,
  draftStep: 2,
};

describe("EditEmployeePage", () => {
  afterEach(cleanup);

  beforeEach(() => {
    mocks.push.mockReset();
    mocks.getEmployee.mockReset();
    mocks.updateEmployee.mockReset();
    mocks.listSalaryStructures.mockReset();
    mocks.listSalaryStructures.mockResolvedValue([]);
    mocks.listPlatformStates.mockReset().mockResolvedValue([
      { id: "st-mh", name: "Maharashtra", code: "MH", isActive: true, sortOrder: 0 },
    ]);
    mocks.listPlatformCities.mockReset().mockResolvedValue([
      { id: "ct-pune", stateId: "st-mh", name: "Pune", isActive: true, sortOrder: 0 },
    ]);
  });

  it("updates an employee and returns to the list", async () => {
    mocks.getEmployee.mockResolvedValue(employee);
    mocks.updateEmployee.mockResolvedValue(employee);
    render(
      <ToastProvider>
        <EditEmployeePage />
      </ToastProvider>,
    );

    expect(await screen.findByDisplayValue("Ada Lovelace")).toBeTruthy();
    expect(screen.getByText(/Ada Lovelace · EMP-01/)).toBeTruthy();
    expect(screen.queryByText(/Loading employee/i)).toBeNull();
    fireEvent.change(screen.getByLabelText(/full name/i), {
      target: { value: "Ada Byron" },
    });
    fireEvent.click(screen.getByRole("button", { name: /^next$/i }));
    fireEvent.click(screen.getByRole("button", { name: /^next$/i }));
    expect(screen.getByLabelText(/overtime rate/i)).toBeTruthy();
    expect(screen.getByRole("checkbox", { name: /covered by provident fund/i })).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: /save changes/i }));

    await vi.waitFor(() => {
      expect(mocks.updateEmployee).toHaveBeenCalledWith(
        "abc",
        expect.objectContaining({
          fullName: "Ada Byron",
          saveAsDraft: false,
          city: "Pune",
          state: "Maharashtra",
        }),
      );
    });
    expect(mocks.push).toHaveBeenCalledWith("/app/employees");
  });

  it("saves a draft without leaving the page", async () => {
    mocks.getEmployee.mockResolvedValue(draftEmployee);
    mocks.updateEmployee.mockResolvedValue(draftEmployee);
    render(
      <ToastProvider>
        <EditEmployeePage />
      </ToastProvider>,
    );

    expect(await screen.findByLabelText(/bank name/i)).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: /save as draft/i }));

    await vi.waitFor(() => {
      expect(mocks.updateEmployee).toHaveBeenCalledWith(
        "abc",
        expect.objectContaining({ saveAsDraft: true, draftStep: 2 }),
      );
    });
    expect(await screen.findByText("Draft saved.")).toBeTruthy();
    expect(mocks.push).not.toHaveBeenCalled();
  });

  it("shows the current salary as a ledger", async () => {
    mocks.getEmployee.mockResolvedValue(employee);
    mocks.listSalaryStructures.mockResolvedValue([
      {
        id: "sal-1",
        employeeId: "abc",
        effectiveFrom: "2026-08-21",
        createdAt: "2026-08-21T00:00:00Z",
        recurringEarnings: 25000,
        recurringDeductions: 0,
        components: [
          {
            id: "c-1",
            name: "Basic Salary",
            type: 0,
            valueType: 0,
            value: 25000,
            sortOrder: 0,
          },
        ],
      },
    ]);
    render(
      <ToastProvider>
        <EditEmployeePage />
      </ToastProvider>,
    );

    expect(await screen.findByRole("heading", { name: "Current structure" })).toBeTruthy();
    expect(screen.getAllByText("₹25,000").length).toBeGreaterThan(0);
    expect(screen.getByText("Earnings")).toBeTruthy();
    expect(screen.getByText("Basic Salary")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Add salary revision" })).toBeTruthy();
  });
});
