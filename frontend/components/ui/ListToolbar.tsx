"use client";

import { Field } from "./Field";
import { Select } from "./Select";

type ListToolbarProps = {
  searchId: string;
  searchLabel: string;
  searchValue: string;
  onSearchChange: (value: string) => void;
  searchPlaceholder?: string;
  filterId: string;
  filterLabel: string;
  filterValue: string;
  filterOptions: Array<{ value: string; label: string }>;
  onFilterChange: (value: string) => void;
  resultText: string;
  onReset?: () => void;
  showReset?: boolean;
};

export function ListToolbar({
  searchId,
  searchLabel,
  searchValue,
  onSearchChange,
  searchPlaceholder,
  filterId,
  filterLabel,
  filterValue,
  filterOptions,
  onFilterChange,
  resultText,
  onReset,
  showReset,
}: ListToolbarProps) {
  return (
    <div className="mp-toolbar">
      <Field id={searchId} label={searchLabel}>
        <input
          className="mp-input"
          type="search"
          value={searchValue}
          placeholder={searchPlaceholder}
          onChange={(event) => onSearchChange(event.target.value)}
        />
      </Field>
      <Field id={filterId} label={filterLabel}>
        <Select
          value={filterValue}
          options={filterOptions}
          onChange={onFilterChange}
        />
      </Field>
      <p className="mp-toolbar__count" role="status" aria-live="polite">
        {resultText}
        {showReset && onReset ? (
          <>
            {" "}
            <button type="button" className="mp-btn mp-btn--ghost" onClick={onReset}>
              Clear
            </button>
          </>
        ) : null}
      </p>
    </div>
  );
}
