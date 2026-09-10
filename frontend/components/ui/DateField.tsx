"use client";

import {
  useEffect,
  useMemo,
  useRef,
  useState,
  type KeyboardEvent,
  type Ref,
} from "react";

const WEEKDAYS = ["Mo", "Tu", "We", "Th", "Fr", "Sa", "Su"];
const DISPLAY = new Intl.DateTimeFormat("en-GB", {
  day: "numeric",
  month: "short",
  year: "numeric",
});
const MONTH = new Intl.DateTimeFormat("en-GB", { month: "long", year: "numeric" });
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

function startOfGrid(month: Date): Date {
  const first = new Date(month.getFullYear(), month.getMonth(), 1);
  const mondayOffset = (first.getDay() + 6) % 7;
  first.setDate(first.getDate() - mondayOffset);
  return first;
}

function shiftMonth(month: Date, delta: number): Date {
  return new Date(month.getFullYear(), month.getMonth() + delta, 1);
}

function sameDay(a: Date, b: Date): boolean {
  return (
    a.getFullYear() === b.getFullYear() &&
    a.getMonth() === b.getMonth() &&
    a.getDate() === b.getDate()
  );
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
  const [month, setMonth] = useState(() => selected ?? today);
  const [cursor, setCursor] = useState(() => selected ?? today);
  const rootRef = useRef<HTMLDivElement>(null);

  const days = useMemo(() => {
    const start = startOfGrid(month);
    return Array.from({ length: 42 }, (_, index) => {
      const date = new Date(start);
      date.setDate(start.getDate() + index);
      return date;
    });
  }, [month]);

  useEffect(() => {
    if (!open) return;

    function onPointerDown(event: PointerEvent) {
      if (!rootRef.current?.contains(event.target as Node)) {
        setOpen(false);
      }
    }

    function onKey(event: globalThis.KeyboardEvent) {
      if (event.key === "Escape" || event.key === "Tab") {
        setOpen(false);
      }
    }

    document.addEventListener("pointerdown", onPointerDown);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("pointerdown", onPointerDown);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  function openCalendar() {
    if (disabled) return;
    const next = selected ?? today;
    setMonth(new Date(next.getFullYear(), next.getMonth(), 1));
    setCursor(next);
    setOpen((current) => !current);
  }

  function isDisabledDay(date: Date): boolean {
    return Boolean(minDate && date < minDate);
  }

  function pick(date: Date) {
    if (disabled || isDisabledDay(date)) return;
    onChange(toIsoDate(date));
    setOpen(false);
  }

  function moveCursor(daysDelta: number) {
    const next = new Date(cursor);
    next.setDate(cursor.getDate() + daysDelta);
    if (isDisabledDay(next)) return;
    setCursor(next);
    setMonth(new Date(next.getFullYear(), next.getMonth(), 1));
  }

  function onTriggerKey(event: KeyboardEvent<HTMLButtonElement>) {
    if (disabled) return;
    if (event.key === "ArrowDown" && !open) {
      event.preventDefault();
      openCalendar();
      return;
    }
    if (!open) return;
    if (event.key === "ArrowLeft") {
      event.preventDefault();
      moveCursor(-1);
    }
    if (event.key === "ArrowRight") {
      event.preventDefault();
      moveCursor(1);
    }
    if (event.key === "ArrowUp") {
      event.preventDefault();
      moveCursor(-7);
    }
    if (event.key === "ArrowDown") {
      event.preventDefault();
      moveCursor(7);
    }
    if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      pick(cursor);
    }
  }

  return (
    <div
      ref={rootRef}
      className="mp-popover"
      aria-invalid={ariaInvalid ? true : undefined}
      aria-required={ariaRequired ? true : undefined}
    >
      <input
        className="mp-date-value"
        type="text"
        name={name}
        value={value}
        tabIndex={-1}
        aria-hidden="true"
        onChange={(event) => onChange(event.target.value)}
      />
      <button
        ref={inputRef}
        id={id}
        type="button"
        className={["mp-date-trigger", className].filter(Boolean).join(" ")}
        disabled={disabled}
        aria-haspopup="dialog"
        aria-expanded={open}
        aria-describedby={ariaDescribedBy}
        onClick={openCalendar}
        onKeyDown={onTriggerKey}
      >
        {formatDateDisplay(value) || "\u00a0"}
      </button>
      {open ? (
        <div className="mp-cal" role="dialog" aria-label="Choose date">
          <div className="mp-cal__header">
            <p className="mp-cal__month">{MONTH.format(month)}</p>
            <div className="mp-cal__nav">
              <button
                type="button"
                className="mp-cal__shift"
                aria-label="Previous month"
                onClick={() => setMonth((current) => shiftMonth(current, -1))}
              >
                ‹
              </button>
              <button
                type="button"
                className="mp-cal__shift"
                aria-label="Next month"
                onClick={() => setMonth((current) => shiftMonth(current, 1))}
              >
                ›
              </button>
            </div>
          </div>
          <div className="mp-cal__week" aria-hidden="true">
            {WEEKDAYS.map((day) => (
              <span key={day}>{day}</span>
            ))}
          </div>
          <div className="mp-cal__grid">
            {days.map((date) => {
              const iso = toIsoDate(date);
              const outside = date.getMonth() !== month.getMonth();
              const blocked = isDisabledDay(date);
              return (
                <button
                  key={iso}
                  type="button"
                  className={[
                    "mp-cal__day",
                    outside ? "is-outside" : "",
                    sameDay(date, today) ? "is-today" : "",
                    selected && sameDay(date, selected) ? "is-selected" : "",
                    sameDay(date, cursor) ? "is-cursor" : "",
                  ]
                    .filter(Boolean)
                    .join(" ")}
                  disabled={blocked}
                  aria-label={DAY_LABEL.format(date)}
                  aria-current={sameDay(date, today) ? "date" : undefined}
                  aria-pressed={selected ? sameDay(date, selected) : undefined}
                  onClick={() => pick(date)}
                >
                  {date.getDate()}
                </button>
              );
            })}
          </div>
          <div className="mp-cal__footer">
            <button
              type="button"
              className="mp-cal__action"
              onClick={() => {
                onChange("");
                setOpen(false);
              }}
            >
              Clear
            </button>
            <button
              type="button"
              className="mp-cal__action"
              disabled={isDisabledDay(today)}
              onClick={() => pick(today)}
            >
              Today
            </button>
          </div>
        </div>
      ) : null}
    </div>
  );
}
