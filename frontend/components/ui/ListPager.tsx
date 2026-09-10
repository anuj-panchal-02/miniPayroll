"use client";

import { useEffect, useState } from "react";
import {
  DEFAULT_PAGE_SIZE,
  PAGE_SIZES,
  clampPage,
  isPageSize,
  pageRange,
  type PageSize,
} from "@/lib/paging";
import { Button } from "./Button";
import { Field } from "./Field";
import { Select } from "./Select";

const SIZE_OPTIONS = PAGE_SIZES.map((size) => ({
  value: String(size),
  label: String(size),
}));

export function usePager(total: number, resetKey = "") {
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState<PageSize>(DEFAULT_PAGE_SIZE);

  useEffect(() => {
    setPage(1);
  }, [resetKey]);

  const current = clampPage(page, total, pageSize);

  function changePageSize(value: string) {
    const next = Number(value);
    if (!isPageSize(next)) {
      return;
    }
    setPageSize(next);
    setPage(1);
  }

  return {
    page: current,
    pageSize,
    setPage,
    setPageSize: changePageSize,
  };
}

type ListPagerProps = {
  id: string;
  page: number;
  pageSize: PageSize;
  total: number;
  onPageChange: (page: number) => void;
  onPageSizeChange: (value: string) => void;
};

export function ListPager({
  id,
  page,
  pageSize,
  total,
  onPageChange,
  onPageSizeChange,
}: ListPagerProps) {
  if (total <= 0) {
    return null;
  }

  const { start, end, pages } = pageRange(page, pageSize, total);

  return (
    <nav className="mp-pager" aria-label="Pagination">
      <Field id={`${id}-rows`} label="Rows">
        <Select
          value={String(pageSize)}
          options={SIZE_OPTIONS}
          onChange={onPageSizeChange}
        />
      </Field>
      <p className="mp-pager__status">
        {start}–{end} of {total}
      </p>
      <div className="mp-pager__nav">
        <Button
          type="button"
          variant="ghost"
          disabled={page <= 1}
          onClick={() => onPageChange(page - 1)}
        >
          Previous
        </Button>
        <Button
          type="button"
          variant="ghost"
          disabled={page >= pages}
          onClick={() => onPageChange(page + 1)}
        >
          Next
        </Button>
      </div>
    </nav>
  );
}
