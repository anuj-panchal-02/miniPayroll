// @vitest-environment jsdom

import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
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

  it("replaces login history after a successful sign in", async () => {
    render(<LoginPage />);

    fireEvent.click(screen.getByRole("button", { name: "Sign in" }));

    await waitFor(() => {
      expect(mocks.replace).toHaveBeenCalledWith("/app");
    });
    expect(mocks.push).not.toHaveBeenCalled();
  });
});
