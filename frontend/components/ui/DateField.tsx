"use client";

import { useMemo, useState, type Ref } from "react";
import { Calendar } from "@/components/shadcn/calendar";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/shadcn/popover";

const DISPLAY = new Intl.DateTimeFormat("en-GB", {
  day: "numeric",
  month: "short",
  year: "numeric",
});
const DAY_LABEL = new Intl.DateTimeFormat("en-GB", {
  day: "numeric",
  month: "long",
  year: "numeric",
});

export function formatDateDisplay(iso: string): string {
  const date = parseIso(iso);
  return date ? DISPLAY.format(date) : "";
}

export function toIsoDate(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, "0");
  const day = String(date.getDate()).padStart(2, "0");
  return `${year}-${month}-${day}`;
}

export function parseIso(iso: string): Date | null {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(iso)) return null;
  const [year, month, day] = iso.split("-").map(Number);
  const date = new Date(year, month - 1, day);
  if (date.getFullYear() !== year || date.getMonth() !== month - 1 || date.getDate() !== day) {
    return null;
  }
  return date;
}

type DateFieldProps = {
  id?: string;
  value: string;
  onChange: (value: string) => void;
  min?: string;
  disabled?: boolean;
  name?: string;
  className?: string;
  inputRef?: Ref<HTMLButtonElement>;
  "aria-invalid"?: boolean | "true" | "false";
  "aria-describedby"?: string;
  "aria-required"?: boolean | "true" | "false";
  autoComplete?: string;
};

export function DateField({
  id,
  value,
  onChange,
  min,
  disabled,
  name,
  className,
  inputRef,
  "aria-invalid": ariaInvalid,
  "aria-describedby": ariaDescribedBy,
  "aria-required": ariaRequired,
}: DateFieldProps) {
  const today = useMemo(() => {
    const now = new Date();
    return new Date(now.getFullYear(), now.getMonth(), now.getDate());
  }, []);
  const selected = parseIso(value);
  const minDate = parseIso(min ?? "");
  const [open, setOpen] = useState(false);
  const todayBlocked = Boolean(minDate && today < minDate);

  function pick(date: Date) {
    if (disabled || (minDate && date < minDate)) {
      return;
    }
    onChange(toIsoDate(date));
    setOpen(false);
  }

  return (
    <div className="mp-popover" aria-invalid={ariaInvalid ? true : undefined} aria-required={ariaRequired ? true : undefined}>
      <input
        className="mp-date-value"
        type="text"
        name={name}
        value={value}
        tabIndex={-1}
        aria-hidden="true"
        onChange={(event) => onChange(event.target.value)}
      />
      <Popover open={open} onOpenChange={(next) => !disabled && setOpen(next)}>
        <PopoverTrigger asChild>
          <button
            ref={inputRef}
            id={id}
            type="button"
            className={["mp-date-trigger", className].filter(Boolean).join(" ")}
            disabled={disabled}
            aria-haspopup="dialog"
            aria-expanded={open}
            aria-describedby={ariaDescribedBy}
          >
            {formatDateDisplay(value) || "\u00a0"}
          </button>
        </PopoverTrigger>
        <PopoverContent aria-label="Choose date">
          <Calendar
            mode="single"
            selected={selected ?? undefined}
            defaultMonth={selected ?? today}
            onSelect={(date) => {
              if (date) {
                pick(date);
              }
            }}
            disabled={minDate ? { before: minDate } : undefined}
            labels={{
              labelDayButton: (date) => DAY_LABEL.format(date),
            }}
          />
          <div className="mp-date-footer">
            <button
              type="button"
              className="mp-date-action"
              onClick={() => {
                onChange("");
                setOpen(false);
              }}
            >
              Clear
            </button>
            <button
              type="button"
              className="mp-date-action"
              disabled={todayBlocked}
              onClick={() => pick(today)}
            >
              Today
            </button>
          </div>
        </PopoverContent>
      </Popover>
    </div>
  );
}
