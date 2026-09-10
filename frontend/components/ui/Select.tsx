"use client";

import {
  useEffect,
  useId,
  useRef,
  useState,
  type KeyboardEvent,
  type Ref,
} from "react";

export type SelectOption = { value: string; label: string };

type SelectProps = {
  id?: string;
  value: string;
  options: SelectOption[];
  onChange: (value: string) => void;
  disabled?: boolean;
  name?: string;
  className?: string;
  inputRef?: Ref<HTMLButtonElement>;
  "aria-invalid"?: boolean | "true" | "false";
  "aria-describedby"?: string;
  "aria-required"?: boolean | "true" | "false";
  autoComplete?: string;
};

export function Select({
  id,
  value,
  options,
  onChange,
  disabled,
  name,
  className,
  inputRef,
  "aria-invalid": ariaInvalid,
  "aria-describedby": ariaDescribedBy,
  "aria-required": ariaRequired,
}: SelectProps) {
  const [open, setOpen] = useState(false);
  const selectedIndex = options.findIndex((option) => option.value === value);
  const [activeIndex, setActiveIndex] = useState(
    selectedIndex >= 0 ? selectedIndex : 0,
  );
  const rootRef = useRef<HTMLDivElement>(null);
  const listId = useId();
  const selected = options.find((option) => option.value === value);

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

  function commit(index: number) {
    const option = options[index];
    if (!option || disabled) return;
    onChange(option.value);
    setOpen(false);
  }

  function toggle() {
    if (disabled) return;
    setActiveIndex(selectedIndex >= 0 ? selectedIndex : 0);
    setOpen((current) => !current);
  }

  function onTriggerKey(event: KeyboardEvent<HTMLButtonElement>) {
    if (disabled) return;
    if (event.key === "ArrowDown" || event.key === "ArrowUp") {
      event.preventDefault();
      if (!open) {
        setActiveIndex(selectedIndex >= 0 ? selectedIndex : 0);
        setOpen(true);
        return;
      }
      if (options.length === 0) return;
      const delta = event.key === "ArrowDown" ? 1 : -1;
      setActiveIndex((current) => (current + delta + options.length) % options.length);
    }
    if (event.key === "Enter" || event.key === " ") {
      event.preventDefault();
      if (open) {
        commit(activeIndex);
      } else {
        setOpen(true);
      }
    }
    if (event.key === "Home") {
      event.preventDefault();
      setActiveIndex(0);
    }
    if (event.key === "End") {
      event.preventDefault();
      setActiveIndex(options.length - 1);
    }
  }

  return (
    <div
      ref={rootRef}
      className="mp-popover"
      aria-invalid={ariaInvalid ? true : undefined}
      aria-required={ariaRequired ? true : undefined}
    >
      {name ? <input type="hidden" name={name} value={value} /> : null}
      <button
        ref={inputRef}
        id={id}
        type="button"
        className={["mp-select-trigger", className].filter(Boolean).join(" ")}
        disabled={disabled}
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-controls={listId}
        aria-describedby={ariaDescribedBy}
        onClick={toggle}
        onKeyDown={onTriggerKey}
      >
        {selected?.label ?? ""}
      </button>
      {open ? (
        <ul
          id={listId}
          className="mp-select-menu"
          role="listbox"
          aria-activedescendant={`${listId}-${activeIndex}`}
        >
          {options.map((option, index) => (
            <li
              id={`${listId}-${index}`}
              key={option.value}
              role="option"
              className={[
                "mp-select-option",
                option.value === value ? "is-selected" : "",
                index === activeIndex ? "is-active" : "",
              ]
                .filter(Boolean)
                .join(" ")}
              aria-selected={option.value === value}
              onMouseEnter={() => setActiveIndex(index)}
              onClick={() => commit(index)}
            >
              {option.label}
            </li>
          ))}
        </ul>
      ) : null}
    </div>
  );
}
