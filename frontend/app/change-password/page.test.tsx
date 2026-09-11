// @vitest-environment jsdom

import { fireEvent, render, screen, waitFor, cleanup } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { CompanySetupStep } from "@/lib/setup";
import { ToastProvider } from "@/components/Toast";
import ChangePasswordPage from "./page";

const mocks = vi.hoisted(() => ({
  replace: vi.fn(),
  push: vi.fn(),
  getToken: vi.fn(),
  getMe: vi.fn(),
  changePassword: vi.fn(),
  setToken: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: mocks.replace, push: mocks.push }),
}));

vi.mock("@/lib/api", () => ({
  getToken: mocks.getToken,
  getMe: mocks.getMe,
  changePassword: mocks.changePassword,
  setToken: mocks.setToken,
}));

vi.mock("@/components/BrandLogo", () => ({
  BrandLogo: () => <span>Logo</span>,
}));

vi.mock("@/components/SignOutButton", () => ({
  SignOutButton: () => <button type="button">Sign out</button>,
}));

const account = {
  email: "owner@abctraders.example",
  roles: ["CompanyAdmin"],
  mustChangePassword: true,
  isSetupComplete: true,
  setupStep: CompanySetupStep.Complete,
};

describe("ChangePasswordPage", () => {
  afterEach(cleanup);

  beforeEach(() => {
    mocks.replace.mockReset();
    mocks.push.mockReset();
    mocks.getToken.mockReset();
    mocks.getMe.mockReset();
    mocks.changePassword.mockReset();
    mocks.setToken.mockReset();
    mocks.getToken.mockReturnValue("token");
    mocks.getMe.mockResolvedValue(account);
    mocks.changePassword.mockResolvedValue(undefined);
  });

  it("redirects to login when there is no token", async () => {
    mocks.getToken.mockReturnValue(null);
    render(
      <ToastProvider>
        <ChangePasswordPage />
      </ToastProvider>,
    );
    await waitFor(() => {
      expect(mocks.replace).toHaveBeenCalledWith("/login");
    });
  });

  it("validates on blur and focuses the first error on submit", async () => {
    render(
      <ToastProvider>
        <ChangePasswordPage />
      </ToastProvider>,
    );
    const current = await screen.findByLabelText("Current password");
    fireEvent.blur(current);
    expect(screen.getByText("Enter your current password.")).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Change password" }));
    expect(document.activeElement).toBe(current);
    expect(mocks.changePassword).not.toHaveBeenCalled();
  });

  it("toggles password visibility", async () => {
    render(
      <ToastProvider>
        <ChangePasswordPage />
      </ToastProvider>,
    );
    const current = (await screen.findByLabelText("Current password")) as HTMLInputElement;
    fireEvent.click(screen.getAllByRole("button", { name: "Show password" })[0]);
    expect(current.type).toBe("text");
  });

  it("saves a valid password and continues into the app", async () => {
    render(
      <ToastProvider>
        <ChangePasswordPage />
      </ToastProvider>,
    );
    await screen.findByLabelText("Current password");
    fireEvent.change(screen.getByLabelText("Current password"), {
      target: { value: "Tmp_TestAdmin1!" },
    });
    fireEvent.change(screen.getByLabelText("New password"), {
      target: { value: "New_TestAdmin1!" },
    });
    fireEvent.change(screen.getByLabelText("Confirm password"), {
      target: { value: "New_TestAdmin1!" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Change password" }));

    await waitFor(() => {
      expect(mocks.changePassword).toHaveBeenCalledWith("Tmp_TestAdmin1!", "New_TestAdmin1!");
    });
    expect(mocks.replace).toHaveBeenCalledWith("/app");
  });

  it("shows API errors and busy semantics", async () => {
    mocks.changePassword.mockImplementation(
      () => new Promise((_, reject) => {
        setTimeout(() => reject(new Error("Current password is incorrect.")), 0);
      }),
    );
    render(
      <ToastProvider>
        <ChangePasswordPage />
      </ToastProvider>,
    );
    await screen.findByLabelText("Current password");
    fireEvent.change(screen.getByLabelText("Current password"), {
      target: { value: "Tmp_TestAdmin1!" },
    });
    fireEvent.change(screen.getByLabelText("New password"), {
      target: { value: "New_TestAdmin1!" },
    });
    fireEvent.change(screen.getByLabelText("Confirm password"), {
      target: { value: "New_TestAdmin1!" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Change password" }));

    expect(screen.getByRole("button", { name: "Saving" }).getAttribute("aria-busy")).toBe("true");
    expect(await screen.findByRole("alert")).toHaveProperty(
      "textContent",
      "Current password is incorrect.",
    );
  });
});
