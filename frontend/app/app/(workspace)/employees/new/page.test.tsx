// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import NewEmployeePage from "./page";

const mocks = vi.hoisted(() => ({
  push: vi.fn(),
  replace: vi.fn(),
  createEmployee: vi.fn(),
  listPlatformStates: vi.fn(),
  listPlatformCities: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: mocks.push, replace: mocks.replace }),
}));

vi.mock("@/lib/api", () => ({
  EmployeeStatus: { Active: 0, Inactive: 1, Draft: 2 },
  SalaryComponentType: { Earning: 0, Deduction: 1 },
  SalaryComponentValueType: { FixedAmount: 0, PercentageOfBasic: 1 },
  createEmployee: mocks.createEmployee,
  listPlatformStates: mocks.listPlatformStates,
  listPlatformCities: mocks.listPlatformCities,
}));

async function fillPersonal() {
  await vi.waitFor(() => expect(mocks.listPlatformStates).toHaveBeenCalled());
  fireEvent.change(screen.getByLabelText(/^employee id$/i), {
    target: { value: "EMP-01" },
  });
  fireEvent.change(screen.getByLabelText(/full name/i), {
    target: { value: "Ada Lovelace" },
  });
  fireEvent.change(screen.getByLabelText(/^email$/i), {
    target: { value: "ada@example.com" },
  });
  fireEvent.change(screen.getByLabelText(/^phone$/i), {
    target: { value: "9876543210" },
  });
  fireEvent.change(screen.getByLabelText(/address line 1/i), {
    target: { value: "Main Road" },
  });
  fireEvent.click(screen.getByLabelText(/^state$/i));
  fireEvent.click(await screen.findByRole("option", { name: "Maharashtra" }));
  fireEvent.click(screen.getByLabelText(/^city$/i));
  fireEvent.click(await screen.findByRole("option", { name: "Pune" }));
  fireEvent.change(screen.getByLabelText(/postal code/i), {
    target: { value: "411001" },
  });
  fireEvent.change(screen.getByLabelText(/^designation$/i), {
    target: { value: "Engineer" },
  });
  fireEvent.change(
    screen.getByLabelText(/joining date/i).parentElement?.querySelector(".mp-date-value") as HTMLInputElement,
    { target: { value: "2026-01-15" } },
  );
}

function fillBank() {
  fireEvent.change(screen.getByLabelText(/bank name/i), {
    target: { value: "HDFC Bank" },
  });
  fireEvent.change(screen.getByLabelText(/bank account number/i), {
    target: { value: "123456789012" },
  });
  fireEvent.change(screen.getByLabelText(/^ifsc$/i), {
    target: { value: "HDFC0001234" },
  });
}

describe("NewEmployeePage", () => {
  afterEach(cleanup);

  beforeEach(() => {
    mocks.push.mockReset();
    mocks.replace.mockReset();
    mocks.createEmployee.mockReset();
    mocks.listPlatformStates.mockReset().mockResolvedValue([
      { id: "st-mh", name: "Maharashtra", code: "MH", isActive: true, sortOrder: 0 },
    ]);
    mocks.listPlatformCities.mockReset().mockResolvedValue([
      { id: "ct-pune", stateId: "st-mh", name: "Pune", isActive: true, sortOrder: 0 },
    ]);
  });

  it("uses the circular back control aligned with the title", () => {
    render(<NewEmployeePage />);

    const back = screen.getByRole("link", { name: "Employees" });
    expect(back.getAttribute("href")).toBe("/app/employees");
    expect(back.className).toContain("sa-back");
    expect(back.closest("header")?.className).toContain("sa-head--with-back");
    expect(screen.getByPlaceholderText("EMP-01")).toBeTruthy();
    expect(screen.getByPlaceholderText("Priya Sharma")).toBeTruthy();
    expect(screen.getByLabelText(/employment type/i)).toHaveProperty("disabled", true);
  });

  it("blocks Next on empty personal details without calling the API", async () => {
    render(<NewEmployeePage />);

    fireEvent.click(screen.getByRole("button", { name: /^next$/i }));

    expect(await screen.findByText("Enter an employee ID.")).toBeTruthy();
    expect(screen.getAllByText("Enter an employee ID.")).toHaveLength(1);
    expect(screen.getByText("Enter a joining date.")).toBeTruthy();
    expect(mocks.createEmployee).not.toHaveBeenCalled();
  });

  it("saves a draft with identity only", async () => {
    mocks.createEmployee.mockResolvedValue({ id: "draft-1" });
    render(<NewEmployeePage />);

    fireEvent.change(screen.getByLabelText(/^employee id$/i), {
      target: { value: "EMP-01" },
    });
    fireEvent.change(screen.getByLabelText(/full name/i), {
      target: { value: "Ada Lovelace" },
    });
    fireEvent.click(screen.getByRole("button", { name: /save as draft/i }));

    await vi.waitFor(() => {
      expect(mocks.createEmployee).toHaveBeenCalledWith(
        expect.objectContaining({
          employeeCode: "EMP-01",
          fullName: "Ada Lovelace",
          saveAsDraft: true,
          draftStep: 1,
        }),
      );
    });
    expect(mocks.replace).toHaveBeenCalledWith("/app/employees/draft-1");
  });

  it("advances from bank details to payroll", async () => {
    render(<NewEmployeePage />);

    await fillPersonal();
    fireEvent.click(screen.getByRole("button", { name: /^next$/i }));
    fillBank();
    fireEvent.click(screen.getByRole("button", { name: /^next$/i }));

    expect(screen.getByLabelText(/overtime rate/i)).toBeTruthy();
  });

  it("saves a draft from bank details", async () => {
    mocks.createEmployee.mockResolvedValue({ id: "draft-2" });
    render(<NewEmployeePage />);

    await fillPersonal();
    fireEvent.click(screen.getByRole("button", { name: /^next$/i }));
    fillBank();
    fireEvent.click(screen.getByRole("button", { name: /save as draft/i }));

    await vi.waitFor(() => {
      expect(mocks.createEmployee).toHaveBeenCalledWith(
        expect.objectContaining({
          employeeCode: "EMP-01",
          saveAsDraft: true,
          draftStep: 2,
        }),
      );
    });
    expect(mocks.replace).toHaveBeenCalledWith("/app/employees/draft-2");
  });

  it("creates a complete employee without saveAsDraft", async () => {
    mocks.createEmployee.mockResolvedValue({ id: "1" });
    render(<NewEmployeePage />);

    await fillPersonal();
    fireEvent.click(screen.getByRole("button", { name: /^next$/i }));
    fillBank();
    fireEvent.click(screen.getByRole("button", { name: /^next$/i }));
    expect(screen.getByLabelText(/overtime rate/i)).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: /^next$/i }));
    expect(screen.getByLabelText(/effective from/i)).toBeTruthy();
    expect(screen.queryByRole("heading", { name: /salary structure/i })).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: /\+ hra/i }));
    expect(screen.getByDisplayValue("HRA")).toBeTruthy();
    fireEvent.change(screen.getAllByLabelText(/^amount$/i)[0], {
      target: { value: "25000" },
    });
    fireEvent.change(screen.getAllByLabelText(/^amount$/i)[1], {
      target: { value: "40" },
    });
    fireEvent.click(screen.getByRole("button", { name: /save employee/i }));

    await vi.waitFor(() => {
      expect(mocks.createEmployee).toHaveBeenCalledWith(
        expect.objectContaining({
          employeeCode: "EMP-01",
          saveAsDraft: false,
          city: "Pune",
          state: "Maharashtra",
        }),
      );
    });
    expect(mocks.push).toHaveBeenCalledWith("/app/employees");
  });

  it("adds a salary preset without repeating the step heading", async () => {
    render(<NewEmployeePage />);

    await fillPersonal();
    fireEvent.click(screen.getByRole("button", { name: /^next$/i }));
    fillBank();
    fireEvent.click(screen.getByRole("button", { name: /^next$/i }));
    fireEvent.click(screen.getByRole("button", { name: /^next$/i }));

    expect(screen.getByLabelText(/effective from/i)).toBeTruthy();
    expect(screen.queryByRole("heading", { name: /salary structure/i })).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: /\+ hra/i }));
    expect(screen.getByDisplayValue("HRA")).toBeTruthy();
    expect(screen.queryByRole("button", { name: /\+ hra/i })).toBeNull();
  });
});
