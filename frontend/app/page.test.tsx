// @vitest-environment jsdom

import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import Home from "./page";

vi.mock("@/components/BrandLogo", () => ({
  BrandLogo: () => <span>Logo</span>,
}));

describe("Home landing", () => {
  afterEach(cleanup);

  it("states the manifesto and sends existing users to login", () => {
    render(<Home />);

    expect(screen.getByRole("heading", { level: 1, name: "Payroll suites got too large." })).toBeTruthy();
    expect(screen.getByRole("img", { name: /payroll review for a finalized month/i })).toBeTruthy();
    expect(screen.getByRole("img", { name: /add employee/i })).toBeTruthy();
    expect(screen.getByRole("img", { name: /monthly inputs table/i })).toBeTruthy();
    expect(screen.getByRole("img", { name: /overtime, bonus, and deduction/i })).toBeTruthy();
    expect(screen.getByRole("img", { name: /payroll history/i })).toBeTruthy();
    const logIn = screen.getAllByRole("link", { name: "Log in" });
    expect(logIn.length).toBeGreaterThan(0);
    for (const link of logIn) {
      expect(link.getAttribute("href")).toBe("/login");
    }
  });
});
