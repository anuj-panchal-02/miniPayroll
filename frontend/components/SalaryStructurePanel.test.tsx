// @vitest-environment jsdom

import { fireEvent, render, screen, waitFor, cleanup } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { SalaryStructurePanel } from "./SalaryStructurePanel";

const mocks = vi.hoisted(() => ({
  listSalaryStructures: vi.fn(),
  createSalaryStructure: vi.fn(),
}));

vi.mock("@/lib/api", () => ({
  SalaryComponentType: { Earning: 0, Deduction: 1 },
  SalaryComponentValueType: { FixedAmount: 0, PercentageOfBasic: 1 },
  BonusType: { Festival: 0, Performance: 1, Attendance: 2, Incentive: 3, Other: 4 },
  OneTimeDeductionType: { AdvanceRecovery: 0, LoanInstallment: 1, Tds: 2, Other: 3 },
  listSalaryStructures: mocks.listSalaryStructures,
  createSalaryStructure: mocks.createSalaryStructure,
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
  employmentType: 0 as const,
  joiningDate: "2026-01-15",
  exitDate: null,
  status: 0 as const,
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

describe("SalaryStructurePanel", () => {
  afterEach(cleanup);

  beforeEach(() => {
    mocks.listSalaryStructures.mockReset();
    mocks.createSalaryStructure.mockReset();
    HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
      this.setAttribute("open", "");
    };
    HTMLDialogElement.prototype.close = function close(this: HTMLDialogElement) {
      this.removeAttribute("open");
    };
  });

  it("shows a load error", async () => {
    mocks.listSalaryStructures.mockRejectedValue(new Error("Could not load salary structures."));
    render(<SalaryStructurePanel employee={employee} />);
    expect(await screen.findByRole("alert")).toHaveProperty(
      "textContent",
      "Could not load salary structures.",
    );
  });

  it("opens a revision editor and can cancel it", async () => {
    mocks.listSalaryStructures.mockResolvedValue([]);
    render(<SalaryStructurePanel employee={employee} />);
    fireEvent.click(await screen.findByRole("button", { name: "Add salary structure" }));
    expect(screen.getByRole("button", { name: "Add salary structure" })).toBeTruthy();
    expect(screen.getByRole("heading", { name: "Ada Lovelace" })).toBeTruthy();
    expect(screen.getByText("EMP-01 · Salary revision")).toBeTruthy();
    expect(screen.getByLabelText(/effective from/i)).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: "Cancel" }));
    expect(screen.queryByLabelText(/effective from/i)).toBeNull();
  });

  it("saves a revision and shows the new ledger", async () => {
    mocks.listSalaryStructures.mockResolvedValue([]);
    mocks.createSalaryStructure.mockResolvedValue({
      id: "sal-1",
      employeeId: "abc",
      effectiveFrom: "2026-01-15",
      createdAt: "2026-01-15T00:00:00Z",
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
    });
    render(<SalaryStructurePanel employee={employee} />);
    fireEvent.click(await screen.findByRole("button", { name: "Add salary structure" }));
    fireEvent.change(screen.getAllByLabelText(/^amount$/i)[0], {
      target: { value: "25000" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Save salary revision" }));

    await waitFor(() => {
      expect(mocks.createSalaryStructure).toHaveBeenCalled();
    });
    expect(await screen.findByRole("heading", { name: "Current structure" })).toBeTruthy();
    expect(screen.getAllByText("₹25,000").length).toBeGreaterThan(0);
  });
});
