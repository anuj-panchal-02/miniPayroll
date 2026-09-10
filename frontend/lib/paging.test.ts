import { describe, expect, it } from "vitest";
import { clampPage, pageCount, pageRange, pageSlice } from "./paging";

describe("paging", () => {
  it("counts pages from a page size", () => {
    expect(pageCount(0, 10)).toBe(1);
    expect(pageCount(10, 10)).toBe(1);
    expect(pageCount(11, 10)).toBe(2);
    expect(pageCount(36, 10)).toBe(4);
  });

  it("clamps the current page to the available range", () => {
    expect(clampPage(0, 36, 10)).toBe(1);
    expect(clampPage(9, 36, 10)).toBe(4);
    expect(clampPage(2, 5, 10)).toBe(1);
  });

  it("describes the visible range", () => {
    expect(pageRange(1, 10, 36)).toEqual({ start: 1, end: 10, pages: 4 });
    expect(pageRange(4, 10, 36)).toEqual({ start: 31, end: 36, pages: 4 });
    expect(pageRange(1, 10, 0)).toEqual({ start: 0, end: 0, pages: 1 });
  });

  it("slices the current page", () => {
    const items = ["a", "b", "c", "d", "e", "f", "g", "h", "i", "j", "k"];
    expect(pageSlice(items, 1, 10)).toEqual(items.slice(0, 10));
    expect(pageSlice(items, 2, 10)).toEqual(["k"]);
    expect(pageSlice(items, 8, 10)).toEqual(["k"]);
  });
});
