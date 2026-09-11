// @vitest-environment jsdom

import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import SetupLayout from "./layout";

const shellProps = vi.hoisted(() => vi.fn());

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

describe("SetupLayout", () => {
  it("protects setup for company admins with incomplete setup allowed", () => {
    render(
      <SetupLayout>
        <p>Setup form</p>
      </SetupLayout>,
    );

    expect(shellProps).toHaveBeenCalledWith(
      expect.objectContaining({
        requiredRole: "CompanyAdmin",
        allowIncompleteSetup: true,
      }),
    );
    expect(screen.getByText("Setup form")).toBeTruthy();
  });
});
