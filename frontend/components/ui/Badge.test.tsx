import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Badge } from "./Badge";

describe("Badge", () => {
  it("renders text content and default tone", () => {
    render(<Badge>Active</Badge>);
    const badge = screen.getByText("Active");
    expect(badge).toBeDefined();
    expect(badge.className).toContain("mp-badge--default");
  });

  it("applies requested tone and size", () => {
    render(
      <Badge tone="success" size="sm">
        Paid
      </Badge>,
    );
    const badge = screen.getByText("Paid");
    expect(badge.className).toContain("mp-badge--success");
    expect(badge.className).toContain("mp-badge--sm");
  });
});
