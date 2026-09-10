// @vitest-environment jsdom

import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import type { ReactNode } from "react";
import { SuperadminNav } from "./SuperadminNav";

vi.mock("next/link", () => ({
  default: ({
    href,
    children,
    ...props
  }: {
    href: string;
    children: ReactNode;
  }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

describe("SuperadminNav", () => {
  afterEach(cleanup);

  it("marks Companies current on company routes and exposes master links", () => {
    render(<SuperadminNav pathname="/superadmin/companies/new" />);

    const nav = screen.getByRole("navigation", { name: "Superadmin" });
    expect(nav).toBeTruthy();
    expect(screen.getByRole("link", { name: "Companies" }).getAttribute("aria-current")).toBe(
      "page",
    );
    expect(screen.getByRole("link", { name: "Plan" }).getAttribute("href")).toBe(
      "/superadmin/plan",
    );
    expect(screen.getByRole("link", { name: "States" }).getAttribute("href")).toBe(
      "/superadmin/masters/states",
    );
    expect(screen.getByRole("link", { name: "Cities" }).getAttribute("href")).toBe(
      "/superadmin/masters/cities",
    );
  });
});
