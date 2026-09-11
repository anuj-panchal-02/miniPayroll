// @vitest-environment jsdom

import { render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import SuperadminLayout from "./layout";

const shellProps = vi.hoisted(() => vi.fn());

vi.mock("@/components/SuperadminShell", () => ({
  SuperadminShell: (props: { children: React.ReactNode }) => {
    shellProps(props);
    return <>{props.children}</>;
  },
}));

describe("SuperadminLayout", () => {
  it("wraps pages in the platform shell once", () => {
    render(
      <SuperadminLayout>
        <p>Companies</p>
      </SuperadminLayout>,
    );

    expect(shellProps).toHaveBeenCalled();
    expect(screen.getByText("Companies")).toBeTruthy();
  });
});
