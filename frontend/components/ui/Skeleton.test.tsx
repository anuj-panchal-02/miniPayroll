import { render } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { Skeleton } from "./Skeleton";

describe("Skeleton", () => {
  it("renders with base mp-skeleton class and aria-hidden", () => {
    const { container } = render(<Skeleton className="h-4 w-32" />);
    const el = container.firstChild as HTMLElement;
    expect(el).toBeDefined();
    expect(el.className).toContain("mp-skeleton");
    expect(el.className).toContain("h-4");
    expect(el.getAttribute("aria-hidden")).toBe("true");
  });
});
