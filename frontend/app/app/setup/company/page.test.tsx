// @vitest-environment jsdom

import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { CompanySetupStep } from "@/lib/setup";
import CompanySetupPage from "./page";

const mocks = vi.hoisted(() => ({
  push: vi.fn(),
  getCompanySetup: vi.fn(),
  uploadCompanyLogo: vi.fn(),
  updateCompanyDetails: vi.fn(),
  listPlatformStates: vi.fn(),
  listPlatformCities: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: mocks.push }),
}));

vi.mock("@/lib/api", () => ({
  getCompanySetup: mocks.getCompanySetup,
  uploadCompanyLogo: mocks.uploadCompanyLogo,
  updateCompanyDetails: mocks.updateCompanyDetails,
  listPlatformStates: mocks.listPlatformStates,
  listPlatformCities: mocks.listPlatformCities,
}));

vi.mock("@/components/SetupWizardShell", () => ({
  SetupWizardShell: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

const setup = {
  name: "Acme Ltd",
  contactEmail: "payroll@acme.test",
  contactPhone: "9876543210",
  addressLine1: "1 Main Street",
  addressLine2: null,
  city: "Pune",
  state: "Maharashtra",
  postalCode: "411001",
  logoUrl: null,
  payrollCycle: "Monthly",
  dailyRateMethod: "CalendarDays" as const,
  workingDaysPerMonth: 26,
  weeklyOffDays: ["Sunday"],
  setupStep: CompanySetupStep.CompanyDetails,
  isSetupComplete: false,
};

describe("CompanySetupPage", () => {
  afterEach(() => {
    cleanup();
    Reflect.deleteProperty(URL, "createObjectURL");
    Reflect.deleteProperty(URL, "revokeObjectURL");
  });

  beforeEach(() => {
    Object.defineProperty(URL, "createObjectURL", {
      configurable: true,
      value: vi.fn(() => "blob:preview"),
    });
    Object.defineProperty(URL, "revokeObjectURL", {
      configurable: true,
      value: vi.fn(),
    });
    mocks.push.mockReset();
    mocks.getCompanySetup.mockReset().mockResolvedValue(setup);
    mocks.uploadCompanyLogo.mockReset().mockResolvedValue({ ...setup, logoUrl: "/logo.png" });
    mocks.updateCompanyDetails
      .mockReset()
      .mockResolvedValue({ ...setup, setupStep: CompanySetupStep.PayrollSettings });
    mocks.listPlatformStates.mockReset().mockResolvedValue([
      { id: "st-mh", name: "Maharashtra", code: "MH", isActive: true, sortOrder: 0 },
    ]);
    mocks.listPlatformCities.mockReset().mockResolvedValue([
      { id: "ct-pune", stateId: "st-mh", name: "Pune", isActive: true, sortOrder: 0 },
    ]);
  });

  it("loads and prefills saved company details", async () => {
    render(<CompanySetupPage />);

    expect(await screen.findByDisplayValue("Acme Ltd")).toBeTruthy();
    expect(screen.getByDisplayValue("payroll@acme.test")).toBeTruthy();
  });

  it("blocks invalid submission and focuses the first invalid field", async () => {
    render(<CompanySetupPage />);
    const name = await screen.findByLabelText(/company name/i);
    fireEvent.change(name, { target: { value: "" } });

    fireEvent.click(screen.getByRole("button", { name: /save and continue/i }));

    await waitFor(() => expect(document.activeElement).toBe(name));
    expect(screen.getByRole("alert").textContent).toContain("company name");
    expect(mocks.uploadCompanyLogo).not.toHaveBeenCalled();
    expect(mocks.updateCompanyDetails).not.toHaveBeenCalled();
  });

  it("uploads a newly selected logo before saving details and navigating", async () => {
    render(<CompanySetupPage />);
    await screen.findByDisplayValue("Acme Ltd");
    const file = new File(["logo"], "logo.png", { type: "image/png" });
    fireEvent.change(screen.getByLabelText(/company logo/i), {
      target: { files: [file] },
    });

    fireEvent.click(screen.getByRole("button", { name: /save and continue/i }));

    await waitFor(() => {
      expect(mocks.push).toHaveBeenCalledWith("/app/setup/payroll");
    });
    expect(mocks.uploadCompanyLogo).toHaveBeenCalledWith(file);
    expect(mocks.uploadCompanyLogo.mock.invocationCallOrder[0]).toBeLessThan(
      mocks.updateCompanyDetails.mock.invocationCallOrder[0],
    );
  });

  it("does not save details when a new logo upload fails", async () => {
    mocks.uploadCompanyLogo.mockRejectedValueOnce(new Error("Logo upload failed."));
    render(<CompanySetupPage />);
    await screen.findByDisplayValue("Acme Ltd");
    fireEvent.change(screen.getByLabelText(/company logo/i), {
      target: {
        files: [new File(["logo"], "logo.png", { type: "image/png" })],
      },
    });

    fireEvent.click(screen.getByRole("button", { name: /save and continue/i }));

    expect(await screen.findByText("Logo upload failed.")).toBeTruthy();
    expect(mocks.updateCompanyDetails).not.toHaveBeenCalled();
    expect(mocks.push).not.toHaveBeenCalled();
  });

  it("posts selected state and city names", async () => {
    mocks.getCompanySetup.mockResolvedValueOnce({
      ...setup,
      city: "",
      state: "",
      logoUrl: "http://localhost:5238/uploads/logo.png",
    });
    render(<CompanySetupPage />);
    await screen.findByDisplayValue("Acme Ltd");

    await waitFor(() => expect(mocks.listPlatformStates).toHaveBeenCalled());
    fireEvent.click(screen.getByLabelText(/^state$/i));
    fireEvent.click(await screen.findByRole("option", { name: "Maharashtra" }));
    await waitFor(() => expect(mocks.listPlatformCities).toHaveBeenCalledWith("st-mh"));
    fireEvent.click(screen.getByLabelText(/^city$/i));
    fireEvent.click(await screen.findByRole("option", { name: "Pune" }));

    fireEvent.click(screen.getByRole("button", { name: /save and continue/i }));

    await waitFor(() => {
      expect(mocks.updateCompanyDetails).toHaveBeenCalledWith(
        expect.objectContaining({ city: "Pune", state: "Maharashtra" }),
      );
    });
  });

  it("saves existing-logo details without uploading again", async () => {
    mocks.getCompanySetup.mockResolvedValueOnce({
      ...setup,
      logoUrl: "http://localhost:5238/uploads/logo.png",
    });
    render(<CompanySetupPage />);
    await screen.findByAltText(/company logo preview/i);

    fireEvent.click(screen.getByRole("button", { name: /save and continue/i }));

    await waitFor(() => expect(mocks.push).toHaveBeenCalledWith("/app/setup/payroll"));
    expect(mocks.uploadCompanyLogo).not.toHaveBeenCalled();
    expect(mocks.updateCompanyDetails).toHaveBeenCalledOnce();
  });

  it("clears the validation alert after the invalid field is corrected", async () => {
    mocks.getCompanySetup.mockResolvedValueOnce({
      ...setup,
      logoUrl: "http://localhost:5238/uploads/logo.png",
    });
    render(<CompanySetupPage />);
    const name = await screen.findByLabelText(/company name/i);
    fireEvent.change(name, { target: { value: "" } });
    fireEvent.click(screen.getByRole("button", { name: /save and continue/i }));
    expect(await screen.findByRole("alert")).toBeTruthy();

    fireEvent.change(name, { target: { value: "Acme Ltd" } });

    expect(screen.queryByRole("alert")).toBeNull();
  });

  it("does not navigate when an in-flight save resolves after unmount", async () => {
    let resolveSave!: (value: typeof setup) => void;
    mocks.getCompanySetup.mockResolvedValueOnce({
      ...setup,
      logoUrl: "http://localhost:5238/uploads/logo.png",
    });
    mocks.updateCompanyDetails.mockReturnValueOnce(
      new Promise((resolve) => {
        resolveSave = resolve;
      }),
    );
    const view = render(<CompanySetupPage />);
    await screen.findByDisplayValue("Acme Ltd");
    fireEvent.click(screen.getByRole("button", { name: /save and continue/i }));
    await waitFor(() => expect(mocks.updateCompanyDetails).toHaveBeenCalledOnce());

    view.unmount();
    await act(async () => {
      resolveSave(setup);
      await Promise.resolve();
    });

    expect(mocks.push).not.toHaveBeenCalled();
  });

  it("revokes each owned object URL once when replaced or unmounted", async () => {
    const createObjectURL = vi
      .fn()
      .mockReturnValueOnce("blob:first")
      .mockReturnValueOnce("blob:second");
    const revokeObjectURL = vi.fn();
    Object.defineProperty(URL, "createObjectURL", {
      configurable: true,
      value: createObjectURL,
    });
    Object.defineProperty(URL, "revokeObjectURL", {
      configurable: true,
      value: revokeObjectURL,
    });
    const view = render(<CompanySetupPage />);
    await screen.findByDisplayValue("Acme Ltd");
    const input = screen.getByLabelText(/company logo/i);

    fireEvent.change(input, {
      target: { files: [new File(["one"], "one.png", { type: "image/png" })] },
    });
    fireEvent.change(input, {
      target: { files: [new File(["two"], "two.png", { type: "image/png" })] },
    });

    expect(revokeObjectURL.mock.calls.filter(([url]) => url === "blob:first")).toHaveLength(1);
    view.unmount();
    expect(revokeObjectURL.mock.calls.filter(([url]) => url === "blob:second")).toHaveLength(1);
  });
});
