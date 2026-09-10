export const PAGE_SIZES = [10, 25, 50, 100] as const;
export const DEFAULT_PAGE_SIZE = 10;

export type PageSize = (typeof PAGE_SIZES)[number];

export function isPageSize(value: number): value is PageSize {
  return (PAGE_SIZES as readonly number[]).includes(value);
}

export function pageCount(total: number, pageSize: number): number {
  if (total <= 0) {
    return 1;
  }
  return Math.ceil(total / pageSize);
}

export function clampPage(page: number, total: number, pageSize: number): number {
  const pages = pageCount(total, pageSize);
  return Math.min(Math.max(1, page), pages);
}

export function pageRange(
  page: number,
  pageSize: number,
  total: number,
): { start: number; end: number; pages: number } {
  if (total <= 0) {
    return { start: 0, end: 0, pages: 1 };
  }
  const pages = pageCount(total, pageSize);
  const current = clampPage(page, total, pageSize);
  const start = (current - 1) * pageSize + 1;
  const end = Math.min(current * pageSize, total);
  return { start, end, pages };
}

export function pageSlice<T>(items: T[], page: number, pageSize: number): T[] {
  const current = clampPage(page, items.length, pageSize);
  const start = (current - 1) * pageSize;
  return items.slice(start, start + pageSize);
}
