// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import SuperadminPage from "./page";

const mocks = vi.hoisted(() => ({
  replace: vi.fn(),
  push: vi.fn(),
  getToken: vi.fn(),
  listCompanies: vi.fn(),
  setToken: vi.fn(),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ replace: mocks.replace, push: mocks.push }),
}));

vi.mock("next/link", () => ({
  default: ({
    href,
    children,
    ...props
  }: {
    href: string;
    children: React.ReactNode;
  }) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

vi.mock("@/lib/api", () => ({
  getToken: mocks.getToken,
  listCompanies: mocks.listCompanies,
  setToken: mocks.setToken,
}));

vi.mock("@/components/SuperadminShell", () => ({
  SuperadminShell: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));

describe("SuperadminPage", () => {
  afterEach(cleanup);

  beforeEach(() => {
    mocks.replace.mockReset();
    mocks.push.mockReset();
    mocks.getToken.mockReset().mockReturnValue("token");
    mocks.listCompanies.mockReset();
    mocks.setToken.mockReset();
  });

  it("pages the company list", async () => {
    mocks.listCompanies.mockResolvedValue(
      Array.from({ length: 11 }, (_, index) => ({
        id: String(index + 1),
        name: `Firm ${index + 1}`,
        contactEmail: `firm${index + 1}@example.com`,
        status: "Active",
        employeeLimit: 9,
        isSetupComplete: true,
        activatedAt: null,
        hasAdmin: true,
      })),
    );

    render(<SuperadminPage />);
    expect(await screen.findByText("Firm 1")).toBeTruthy();
    expect(screen.getByText("Firm 10")).toBeTruthy();
    expect(screen.queryByText("Firm 11")).toBeNull();
    expect(screen.getByText("1–10 of 11")).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "Next" }));
    expect(screen.getByText("Firm 11")).toBeTruthy();
    expect(screen.queryByText("Firm 1")).toBeNull();
  });
});
