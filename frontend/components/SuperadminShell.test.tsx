// @vitest-environment jsdom

import { useLayoutEffect } from "react";
import {
  act,
  cleanup,
  render,
  screen,
  waitFor,
} from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { CompanySetupStep } from "@/lib/setup";
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

describe("SuperadminShell", () => {
  afterEach(cleanup);

  beforeEach(() => {
    mocks.pathname = "/superadmin";
    mocks.replace.mockReset();
    mocks.push.mockReset();
    mocks.getMe.mockReset();
    mocks.getToken.mockReset();
    mocks.setToken.mockReset();
    mocks.getToken.mockReturnValue("token");
    mocks.getMe.mockResolvedValue({
      roles: ["Superadmin"],
      mustChangePassword: false,
      isSetupComplete: true,
      setupStep: CompanySetupStep.Complete,
    });
  });

  it("announces loading while protected access is being validated", () => {
    mocks.getMe.mockImplementationOnce(() => new Promise(() => undefined));

    render(
      <SuperadminShell>
        <p>Protected content</p>
      </SuperadminShell>,
    );

    expect(screen.getByRole("status").textContent).toMatch(/validating access/i);
    expect(screen.queryByText("Protected content")).toBeNull();
  });

  it("hides stale protected content while a changed route is revalidated", async () => {
    const view = render(
      <SuperadminShell>
        <p>Protected content</p>
      </SuperadminShell>,
    );
    await waitFor(() => {
      expect(screen.getByText("Protected content")).toBeTruthy();
    });
    expect(screen.queryByRole("navigation", { name: /company/i })).toBeNull();
    expect(screen.queryByText(/coming soon/i)).toBeNull();

    mocks.pathname = "/superadmin/companies/new";
    mocks.getMe.mockImplementationOnce(() => new Promise(() => undefined));
    view.rerender(
      <SuperadminShell>
        <p>Protected content</p>
      </SuperadminShell>,
    );

    expect(screen.queryByText("Protected content")).toBeNull();
  });

  it("does not commit stale children before guard effects run", async () => {
    let staleContentCommitted = false;

    function ProtectedContent() {
      useLayoutEffect(() => {
        if (mocks.pathname === "/superadmin/companies/new") {
          staleContentCommitted = true;
        }
      });
      return <p>Protected content</p>;
    }

    const view = render(
      <SuperadminShell>
        <ProtectedContent />
      </SuperadminShell>,
    );
    await waitFor(() => {
      expect(screen.getByText("Protected content")).toBeTruthy();
    });

    mocks.pathname = "/superadmin/companies/new";
    mocks.getMe.mockImplementationOnce(() => new Promise(() => undefined));
    view.rerender(
      <SuperadminShell>
        <ProtectedContent />
      </SuperadminShell>,
    );

    expect(staleContentCommitted).toBe(false);
    expect(screen.queryByText("Protected content")).toBeNull();
  });

  it("does not let an obsolete async response validate the current guard", async () => {
    const account = {
      roles: ["Superadmin"],
      mustChangePassword: false,
      isSetupComplete: true,
      setupStep: CompanySetupStep.Complete,
    };
    let resolveFirst!: (value: typeof account) => void;
    let resolveSecond!: (value: typeof account) => void;
    mocks.getMe
      .mockImplementationOnce(
        () =>
          new Promise((resolve) => {
            resolveFirst = resolve;
          }),
      )
      .mockImplementationOnce(
        () =>
          new Promise((resolve) => {
            resolveSecond = resolve;
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

    await act(async () => {
      resolveFirst(account);
      await Promise.resolve();
    });
    expect(screen.queryByText("Protected content")).toBeNull();

    await act(async () => {
      resolveSecond(account);
      await Promise.resolve();
    });
    expect(screen.getByText("Protected content")).toBeTruthy();
  });
});
