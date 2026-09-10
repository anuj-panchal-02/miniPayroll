// @vitest-environment jsdom

import { fireEvent, render, screen, waitFor, cleanup } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import CreateCompanyPage from "./page";

const mocks = vi.hoisted(() => ({
  replace: vi.fn(),
  push: vi.fn(),
  getToken: vi.fn(),
  getPlatformLimits: vi.fn(),
  createCompany: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: mocks.replace, push: mocks.push }),
}));

vi.mock("@/lib/api", () => ({
  getToken: mocks.getToken,
  getPlatformLimits: mocks.getPlatformLimits,
  createCompany: mocks.createCompany,
}));

vi.mock("@/components/SuperadminShell", () => ({
  SuperadminShell: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

describe("CreateCompanyPage", () => {
  afterEach(cleanup);

  beforeEach(() => {
    mocks.replace.mockReset();
    mocks.push.mockReset();
    mocks.getToken.mockReset();
    mocks.getPlatformLimits.mockReset();
    mocks.createCompany.mockReset();
    mocks.getToken.mockReturnValue("token");
    mocks.getPlatformLimits.mockResolvedValue({
      minEmployeeLimit: 1,
      hardEmployeeCap: 50,
      defaultEmployeeLimit: 50,
      defaultPlanName: "Basic",
      currencyCode: "INR",
    });
    mocks.createCompany.mockResolvedValue({ id: "co-1" });
  });

  it("blocks invalid names and focuses the first field", async () => {
    render(<CreateCompanyPage />);
    const name = await screen.findByLabelText("Company name");
    fireEvent.click(screen.getByRole("button", { name: "Create company" }));
    expect(document.activeElement).toBe(name);
    expect(screen.getByText("Enter a company name.")).toBeTruthy();
    expect(mocks.createCompany).not.toHaveBeenCalled();
  });

  it("rejects limits above the platform cap", async () => {
    render(<CreateCompanyPage />);
    await screen.findByLabelText("Company name");
    fireEvent.change(screen.getByLabelText("Company name"), {
      target: { value: "ABC Traders" },
    });
    fireEvent.change(screen.getByLabelText("Contact email"), {
      target: { value: "owner@abctraders.example" },
    });
    fireEvent.change(screen.getByLabelText("Employee limit"), {
      target: { value: "51" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Create company" }));
    expect(screen.getByText("Employee limit must be between 1 and 50.")).toBeTruthy();
    expect(mocks.createCompany).not.toHaveBeenCalled();
  });

  it("creates a company and opens the detail page", async () => {
    render(<CreateCompanyPage />);
    await screen.findByLabelText("Company name");
    fireEvent.change(screen.getByLabelText("Company name"), {
      target: { value: "ABC Traders" },
    });
    fireEvent.change(screen.getByLabelText("Contact email"), {
      target: { value: "owner@abctraders.example" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Create company" }));

    await waitFor(() => {
      expect(mocks.createCompany).toHaveBeenCalledWith({
        name: "ABC Traders",
        contactEmail: "owner@abctraders.example",
        employeeLimit: 50,
      });
    });
    expect(mocks.push).toHaveBeenCalledWith("/superadmin/companies/co-1");
  });

  it("shows a loading label while creating", async () => {
    mocks.createCompany.mockImplementation(() => new Promise(() => undefined));
    render(<CreateCompanyPage />);
    await screen.findByLabelText("Company name");
    fireEvent.change(screen.getByLabelText("Company name"), {
      target: { value: "ABC Traders" },
    });
    fireEvent.change(screen.getByLabelText("Contact email"), {
      target: { value: "owner@abctraders.example" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Create company" }));
    expect(screen.getByRole("button", { name: "Creating" }).getAttribute("aria-busy")).toBe("true");
  });

  it("surfaces create failures", async () => {
    mocks.createCompany.mockRejectedValue(new Error("A company with this email already exists."));
    render(<CreateCompanyPage />);
    await screen.findByLabelText("Company name");
    fireEvent.change(screen.getByLabelText("Company name"), {
      target: { value: "ABC Traders" },
    });
    fireEvent.change(screen.getByLabelText("Contact email"), {
      target: { value: "owner@abctraders.example" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Create company" }));
    expect(await screen.findByRole("alert")).toHaveProperty(
      "textContent",
      "A company with this email already exists.",
    );
  });
});
