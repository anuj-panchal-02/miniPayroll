// @vitest-environment jsdom

import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { CompanyAdminShell } from "./CompanyAdminShell";

const shellProps = vi.hoisted(() => vi.fn());
const mocks = vi.hoisted(() => ({ pathname: "/app/employees" }));

vi.mock("next/navigation", () => ({
  usePathname: () => mocks.pathname,
}));

vi.mock("@/components/SuperadminShell", () => ({
  SuperadminShell: (props: {
    children: React.ReactNode;
    requiredRole?: string;
    allowIncompleteSetup?: boolean;
  }) => {
    shellProps(props);
    return <>{props.children}</>;
  },
}));

describe("CompanyAdminShell", () => {
  it("protects the workspace without allowing incomplete setup", () => {
    render(
      <CompanyAdminShell>
        <p>Workspace</p>
      </CompanyAdminShell>,
    );

    expect(shellProps).toHaveBeenCalledWith(
      expect.objectContaining({
        requiredRole: "CompanyAdmin",
      }),
    );
    expect(shellProps.mock.calls[0][0].allowIncompleteSetup).toBeUndefined();
    expect(screen.getByRole("navigation", { name: /company/i })).toBeTruthy();
    expect(screen.queryByRole("navigation", { name: /setup progress/i })).toBeNull();
    expect(screen.getByRole("link", { name: "Employees" }).getAttribute("aria-current")).toBe(
      "page",
    );
    expect(screen.getByRole("link", { name: "Payroll" })).toBeTruthy();
    expect(screen.queryByText(/payroll · coming soon/i)).toBeNull();
    expect(screen.getByText(/settings · coming soon/i)).toBeTruthy();
    expect(screen.getByText("Workspace")).toBeTruthy();
  });
});
