// @vitest-environment jsdom

import { fireEvent, render, screen, waitFor, cleanup } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { CompanySetupStep } from "@/lib/setup";
import LoginPage from "./page";

const mocks = vi.hoisted(() => ({
  replace: vi.fn(),
  push: vi.fn(),
  login: vi.fn(),
  setToken: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: mocks.replace, push: mocks.push }),
}));

vi.mock("@/lib/api", () => ({
  login: mocks.login,
  setToken: mocks.setToken,
}));

vi.mock("@/components/BrandLogo", () => ({
  BrandLogo: () => <span>Logo</span>,
}));

describe("LoginPage", () => {
  afterEach(cleanup);

  beforeEach(() => {
    mocks.replace.mockReset();
    mocks.push.mockReset();
    mocks.login.mockReset();
    mocks.setToken.mockReset();
    mocks.login.mockResolvedValue({
      token: "token",
      roles: ["CompanyAdmin"],
      mustChangePassword: false,
      isSetupComplete: true,
      setupStep: CompanySetupStep.Complete,
    });
  });

  it("does not prefill Superadmin credentials", () => {
    render(<LoginPage />);
    expect((screen.getByLabelText("Email") as HTMLInputElement).value).toBe("");
    expect((screen.getByLabelText("Password") as HTMLInputElement).value).toBe("");
  });

  it("validates on blur and focuses the first invalid field", () => {
    render(<LoginPage />);
    const email = screen.getByLabelText("Email");
    fireEvent.blur(email);
    expect(screen.getByText("Enter your email.")).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Sign in" }));
    expect(document.activeElement).toBe(email);
    expect(mocks.login).not.toHaveBeenCalled();
  });

  it("toggles password visibility", () => {
    render(<LoginPage />);
    const password = screen.getByLabelText("Password") as HTMLInputElement;
    expect(password.type).toBe("password");
    fireEvent.click(screen.getByRole("button", { name: "Show password" }));
    expect(password.type).toBe("text");
  });

  it("submits from the keyboard and replaces login history", async () => {
    render(<LoginPage />);
    fireEvent.change(screen.getByLabelText("Email"), {
      target: { value: "owner@abctraders.example" },
    });
    fireEvent.change(screen.getByLabelText("Password"), {
      target: { value: "Tmp_TestAdmin1!" },
    });
    fireEvent.submit(screen.getByRole("form", { name: "Sign in" }));

    await waitFor(() => {
      expect(mocks.replace).toHaveBeenCalledWith("/app");
    });
    expect(mocks.login).toHaveBeenCalledWith(
      "owner@abctraders.example",
      "Tmp_TestAdmin1!",
    );
    expect(mocks.push).not.toHaveBeenCalled();
  });

  it("shows an actionable API error and busy state", async () => {
    mocks.login.mockImplementation(
      () => new Promise((_, reject) => {
        setTimeout(() => reject(new Error("Invalid email or password.")), 0);
      }),
    );
    render(<LoginPage />);
    fireEvent.change(screen.getByLabelText("Email"), {
      target: { value: "owner@abctraders.example" },
    });
    fireEvent.change(screen.getByLabelText("Password"), {
      target: { value: "wrong" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Sign in" }));

    expect(screen.getByRole("button", { name: "Signing in" }).getAttribute("aria-busy")).toBe("true");
    expect(await screen.findByRole("alert")).toHaveProperty(
      "textContent",
      "Invalid email or password.",
    );
  });

  it("replaces login history after a successful sign in", async () => {
    render(<LoginPage />);
    fireEvent.change(screen.getByLabelText("Email"), {
      target: { value: "owner@abctraders.example" },
    });
    fireEvent.change(screen.getByLabelText("Password"), {
      target: { value: "Tmp_TestAdmin1!" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Sign in" }));

    await waitFor(() => {
      expect(mocks.replace).toHaveBeenCalledWith("/app");
    });
  });
});
