// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { ListPager } from "./ListPager";

describe("ListPager", () => {
  afterEach(cleanup);

  it("shows the visible range and walks pages", () => {
    const onPageChange = vi.fn();
    render(
      <ListPager
        id="states"
        page={1}
        pageSize={10}
        total={36}
        onPageChange={onPageChange}
        onPageSizeChange={() => undefined}
      />,
    );

    expect(screen.getByText("1–10 of 36")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Previous" })).toHaveProperty("disabled", true);
    fireEvent.click(screen.getByRole("button", { name: "Next" }));
    expect(onPageChange).toHaveBeenCalledWith(2);
  });

  it("changes the page size", () => {
    const onPageSizeChange = vi.fn();
    render(
      <ListPager
        id="states"
        page={1}
        pageSize={10}
        total={36}
        onPageChange={() => undefined}
        onPageSizeChange={onPageSizeChange}
      />,
    );

    fireEvent.click(screen.getByLabelText(/^rows$/i));
    fireEvent.click(screen.getByRole("option", { name: "50" }));
    expect(onPageSizeChange).toHaveBeenCalledWith("50");
  });
});
