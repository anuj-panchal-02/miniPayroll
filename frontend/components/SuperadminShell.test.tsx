// @vitest-environment jsdom

import {
  act,
  cleanup,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { CompanySetupStep } from "@/lib/setup";
import { clearSession, loadSession } from "@/lib/session";
import { SuperadminShell } from "./SuperadminShell";

const mocks = vi.hoisted(() => ({
  pathname: "/superadmin",
  replace: vi.fn(),
  push: vi.fn(),
  getMe: vi.fn(),
  getToken: vi.fn(),
  setToken: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  usePathname: () => mocks.pathname,
  useRouter: () => ({ replace: mocks.replace, push: mocks.push }),
}));

vi.mock("@/lib/api", () => ({
  getMe: mocks.getMe,
  getToken: mocks.getToken,
  setToken: mocks.setToken,
}));

vi.mock("@/components/BrandLogo", () => ({
  BrandLogo: () => <span>Logo</span>,
}));

vi.mock("@/components/SignOutButton", () => ({
  SignOutButton: () => <button type="button">Sign out</button>,
}));

const superadmin = {
  id: "user-1",
  email: "root@example.com",
  roles: ["Superadmin"],
  companyId: null,
  mustChangePassword: false,
  twoFactorEnabled: false,
  isSetupComplete: true,
  setupStep: CompanySetupStep.Complete,
};

describe("SuperadminShell", () => {
  afterEach(() => {
    cleanup();
    clearSession();
  });

  beforeEach(() => {
    clearSession();
    mocks.pathname = "/superadmin";
    mocks.replace.mockReset();
    mocks.push.mockReset();
    mocks.getMe.mockReset();
    mocks.getToken.mockReset();
    mocks.setToken.mockReset();
    mocks.getToken.mockReturnValue("token");
    mocks.getMe.mockResolvedValue(superadmin);
  });

  it("announces loading while protected access is being validated", () => {
    mocks.getMe.mockImplementationOnce(() => new Promise(() => undefined));

    render(
      <SuperadminShell>
        <p>Protected content</p>
      </SuperadminShell>,
    );

    expect(screen.getByRole("status").textContent).toMatch(/validating access/i);
    expect(screen.getByRole("navigation", { name: "Superadmin" })).toBeTruthy();
    expect(screen.queryByText("Protected content")).toBeNull();
  });

  it("keeps nav and children on a same-role path change without waiting on getMe", async () => {
    const view = render(
      <SuperadminShell>
        <p>Protected content</p>
      </SuperadminShell>,
    );
    await waitFor(() => {
      expect(screen.getByText("Protected content")).toBeTruthy();
    });
    expect(screen.getByRole("navigation", { name: "Superadmin" })).toBeTruthy();
    expect(screen.getByRole("link", { name: "States" })).toBeTruthy();
    expect(screen.getByRole("link", { name: "Cities" })).toBeTruthy();
    expect(mocks.getMe).toHaveBeenCalledTimes(1);

    mocks.pathname = "/superadmin/companies/new";
    mocks.getMe.mockImplementationOnce(() => new Promise(() => undefined));
    view.rerender(
      <SuperadminShell>
        <p>Protected content</p>
      </SuperadminShell>,
    );

    expect(screen.getByText("Protected content")).toBeTruthy();
    expect(screen.getByRole("navigation", { name: "Superadmin" })).toBeTruthy();
    expect(mocks.getMe).toHaveBeenCalledTimes(1);
  });

  it("hides the platform masters nav for company admin", async () => {
    mocks.pathname = "/app";
    mocks.getMe.mockResolvedValue({
      ...superadmin,
      roles: ["CompanyAdmin"],
      companyId: "co-1",
    });

    render(
      <SuperadminShell
        role="Company Admin"
        homeHref="/app"
        requiredRole="CompanyAdmin"
      >
        <p>Protected content</p>
      </SuperadminShell>,
    );

    await waitFor(() => {
      expect(screen.getByText("Protected content")).toBeTruthy();
    });
    expect(screen.queryByRole("navigation", { name: "Superadmin" })).toBeNull();
    expect(screen.queryByRole("link", { name: "States" })).toBeNull();
    expect(screen.queryByRole("link", { name: "Cities" })).toBeNull();
  });

  it("does not let an obsolete async response paint children after unmount", async () => {
    let resolveMe!: (value: typeof superadmin) => void;
    mocks.getMe.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          resolveMe = resolve;
        }),
    );

    const view = render(
      <SuperadminShell>
        <p>Protected content</p>
      </SuperadminShell>,
    );
    view.unmount();

    await act(async () => {
      resolveMe(superadmin);
      await Promise.resolve();
    });
    expect(screen.queryByText("Protected content")).toBeNull();
  });

  it("applies the resolved session to the current path after a first-load race", async () => {
    let resolveMe!: (value: typeof superadmin) => void;
    mocks.getMe.mockImplementationOnce(
      () =>
        new Promise((resolve) => {
          resolveMe = resolve;
        }),
    );

    const view = render(
      <SuperadminShell>
        <p>Protected content</p>
      </SuperadminShell>,
    );
    mocks.pathname = "/superadmin/companies/new";
    view.rerender(
      <SuperadminShell>
        <p>Protected content</p>
      </SuperadminShell>,
    );

    expect(screen.queryByText("Protected content")).toBeNull();

    await act(async () => {
      resolveMe(superadmin);
      await Promise.resolve();
    });
    expect(screen.getByText("Protected content")).toBeTruthy();
    expect(mocks.getMe).toHaveBeenCalledTimes(1);
  });

  it("redirects a cached session with the wrong role and does not paint children", async () => {
    mocks.getMe.mockResolvedValue({
      ...superadmin,
      roles: ["CompanyAdmin"],
      companyId: "co-1",
    });
    await loadSession();
    mocks.pathname = "/superadmin";

    render(
      <SuperadminShell>
        <p>Protected content</p>
      </SuperadminShell>,
    );

    expect(screen.queryByText("Protected content")).toBeNull();
    await waitFor(() => {
      expect(mocks.replace).toHaveBeenCalledWith("/app");
    });
  });
});
