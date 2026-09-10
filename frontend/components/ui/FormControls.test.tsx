// @vitest-environment jsdom

import { readFileSync } from "node:fs";
import { join } from "node:path";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { Alert } from "./Alert";
import { Button } from "./Button";
import { Choice } from "./Choice";
import { DateField } from "./DateField";
import { Field } from "./Field";
import { FileField } from "./FileField";
import { ListToolbar } from "./ListToolbar";
import { PasswordField } from "./PasswordField";
import { Select } from "./Select";
import { Stepper } from "./Stepper";
import { PASSWORD_RULE_MESSAGE } from "@/lib/validation";

describe("form primitives", () => {
  afterEach(cleanup);
  it("associates the field label, hint, and error slot", () => {
    const { rerender } = render(
      <Field id="email" label="Email" hint="Work email" required>
        <input className="mp-input" />
      </Field>,
    );

    const input = screen.getByLabelText("Email");
    expect(input.getAttribute("aria-describedby")).toBe("email-hint");
    expect(input.getAttribute("aria-required")).toBe("true");
    expect(screen.getByText("Work email")).toBeTruthy();
    expect(input.closest(".mp-field__well")).toBeTruthy();
    expect(screen.getByText("Email", { selector: "label" }).closest(".mp-field__well")).toBeNull();
    expect(input.getAttribute("autocomplete")).toBe("off");

    rerender(
      <Field id="email" label="Email" hint="Work email" error="Enter a valid email." required>
        <input className="mp-input" />
      </Field>,
    );

    expect(input.getAttribute("aria-invalid")).toBe("true");
    expect(input.getAttribute("aria-describedby")).toContain("email-error");
    expect(screen.getByText("Enter a valid email.")).toBeTruthy();
    expect(screen.queryByText("Work email")).toBeNull();
  });

  it("tones the field slot for success and lets an error take it back", () => {
    const { container, rerender } = render(
      <Field id="email" label="Email" hint="Work email" success="Address verified.">
        <input className="mp-input" />
      </Field>,
    );

    const slot = container.querySelector(".mp-field__slot");
    expect(slot?.getAttribute("data-tone")).toBe("success");
    expect(slot?.textContent).toBe("Address verified.");
    expect(container.querySelector(".mp-field__well")?.className).toContain("is-success");

    rerender(
      <Field
        id="email"
        label="Email"
        hint="Work email"
        success="Address verified."
        error="Enter a valid email."
      >
        <input className="mp-input" />
      </Field>,
    );

    const well = container.querySelector(".mp-field__well");
    expect(container.querySelector(".mp-field__slot")?.getAttribute("data-tone")).toBe("error");
    expect(well?.className).toContain("is-error");
    expect(well?.className).not.toContain("is-success");
  });

  it("exposes loading semantics on buttons", () => {
    render(
      <Button loading loadingLabel="Saving">
        Save
      </Button>,
    );

    const button = screen.getByRole("button", { name: "Saving" });
    expect(button.getAttribute("aria-busy")).toBe("true");
    expect(button).toHaveProperty("disabled", true);
  });

  it("toggles password visibility without losing the accessible name", () => {
    render(
      <PasswordField
        id="secret"
        label="Password"
        hint="Keep this private."
        value="secret"
        onChange={() => undefined}
      />,
    );

    const field = screen.getByLabelText("Password") as HTMLInputElement;
    const toggle = screen.getByRole("button", { name: "Show password" });
    expect(field.type).toBe("password");
    expect(toggle.closest(".mp-field__well")).toBeTruthy();
    fireEvent.click(toggle);
    expect(field.type).toBe("text");
    expect(screen.getByRole("button", { name: "Hide password" }).getAttribute("aria-pressed")).toBe(
      "true",
    );
  });

  it("holds the strength meter back until the password field has a value", () => {
    const { container, rerender } = render(
      <PasswordField id="secret" label="Password" value="" onChange={() => undefined} />,
    );

    expect(container.querySelector(".mp-field__meter")).toBeNull();

    rerender(
      <PasswordField id="secret" label="Password" value="design" onChange={() => undefined} />,
    );

    const meter = container.querySelector(".mp-field__meter");
    expect(meter?.getAttribute("data-tone")).toBe("error");
    expect(meter?.textContent).toContain("Password too weak");
    expect(screen.getByLabelText("Password").getAttribute("aria-describedby")).toContain(
      "secret-strength",
    );

    rerender(
      <PasswordField
        id="secret"
        label="Password"
        value="Design.dey123!"
        onChange={() => undefined}
      />,
    );

    const strong = container.querySelector(".mp-field__meter");
    expect(strong?.getAttribute("data-tone")).toBe("success");
    expect(strong?.textContent).toBe("Password strong");
  });

  it("does not repeat the password rule when the field slot already carries it", () => {
    const { container } = render(
      <PasswordField
        id="new-password"
        label="New password"
        hint={PASSWORD_RULE_MESSAGE}
        value="design"
        onChange={() => undefined}
      />,
    );

    expect(container.querySelector(".mp-field__meter-label")?.textContent).toBe(
      "Password too weak",
    );
    expect(screen.getAllByText(PASSWORD_RULE_MESSAGE)).toHaveLength(1);
  });

  it("renders an affix mark alongside the field value", () => {
    const { container } = render(
      <Field id="amount" label="Amount" affix="₹">
        <input className="mp-input" defaultValue="25000" />
      </Field>,
    );

    expect(container.querySelector(".mp-field__well--affix")).toBeTruthy();
    expect(container.querySelector(".mp-affix__mark")?.textContent).toBe("₹");
    expect(screen.getByLabelText("Amount")).toHaveProperty("value", "25000");
  });

  it("keeps the file well labelled and shows the chosen name", () => {
    const { container } = render(
      <FileField id="logo" label="Company logo" hint="PNG or SVG." />,
    );

    const input = screen.getByLabelText(/company logo/i) as HTMLInputElement;
    expect(input.closest(".mp-field__well")).toBeTruthy();
    expect(screen.getByText("Company logo", { selector: "label" }).closest(".mp-field__well")).toBeNull();
    expect(container.querySelector(".mp-file-well")).toBeTruthy();
    expect(screen.getByText("Choose a file")).toBeTruthy();
  });

  it("announces list filter results and resets them", () => {
    const onReset = vi.fn();
    render(
      <ListToolbar
        searchId="search"
        searchLabel="Search companies"
        searchValue="acme"
        onSearchChange={() => undefined}
        filterId="status"
        filterLabel="Status"
        filterValue="Pending"
        filterOptions={[
          { value: "all", label: "All statuses" },
          { value: "Pending", label: "Pending" },
        ]}
        onFilterChange={() => undefined}
        resultText="1 company"
        showReset
        onReset={onReset}
      />,
    );

    expect(screen.getByRole("status").textContent).toContain("1 company");
    fireEvent.click(screen.getByRole("button", { name: "Clear" }));
    expect(onReset).toHaveBeenCalledOnce();
  });

  it("opens a listbox and commits the chosen option", () => {
    const onChange = vi.fn();
    render(
      <Field id="status" label="Status">
        <Select
          value="Pending"
          options={[
            { value: "all", label: "All statuses" },
            { value: "Pending", label: "Pending" },
          ]}
          onChange={onChange}
        />
      </Field>,
    );

    fireEvent.click(screen.getByLabelText("Status"));
    const listbox = screen.getByRole("listbox");
    expect(listbox.closest(".mp-field__well")).toBeTruthy();
    expect(screen.getByRole("option", { name: "Pending" }).className).toContain("is-selected");
    expect(screen.getByRole("option", { name: "Pending" }).className).toContain("is-active");
    fireEvent.click(screen.getByRole("option", { name: "All statuses" }));
    expect(onChange).toHaveBeenCalledWith("all");
  });

  it("closes the listbox on Escape", () => {
    render(
      <Field id="status" label="Status">
        <Select
          value="Pending"
          options={[
            { value: "Pending", label: "Pending" },
            { value: "Active", label: "Active" },
          ]}
          onChange={() => undefined}
        />
      </Field>,
    );

    fireEvent.click(screen.getByLabelText("Status"));
    expect(screen.getByRole("listbox")).toBeTruthy();
    fireEvent.keyDown(document, { key: "Escape" });
    expect(screen.queryByRole("listbox")).toBeNull();
  });

  it("picks a calendar day as an ISO date", () => {
    const onChange = vi.fn();
    render(
      <Field id="join" label="Joining date">
        <DateField value="2026-01-15" onChange={onChange} />
      </Field>,
    );

    expect(screen.getByLabelText("Joining date").textContent).toBe("15 Jan 2026");
    fireEvent.click(screen.getByLabelText("Joining date"));
    fireEvent.click(screen.getByRole("button", { name: "20 January 2026" }));
    expect(onChange).toHaveBeenCalledWith("2026-01-20");
  });

  it("blocks calendar days before min", () => {
    render(
      <Field id="from" label="Effective from">
        <DateField value="2026-01-20" min="2026-01-15" onChange={() => undefined} />
      </Field>,
    );

    fireEvent.click(screen.getByLabelText("Effective from"));
    expect(screen.getByRole("button", { name: "14 January 2026" })).toHaveProperty("disabled", true);
    expect(screen.getByRole("button", { name: "15 January 2026" })).toHaveProperty("disabled", false);
  });

  it("keeps choice cards keyboard operable", () => {
    const onChange = vi.fn();
    render(
      <Choice type="checkbox" label="Sunday" checked={false} onChange={onChange} />,
    );

    const input = screen.getByLabelText("Sunday");
    input.focus();
    expect(document.activeElement).toBe(input);
    fireEvent.click(input);
    expect(onChange).toHaveBeenCalled();
  });

  it("renders success alerts with status, not error colouring", () => {
    render(<Alert tone="success">Draft saved.</Alert>);
    expect(screen.getByRole("status").textContent).toBe("Draft saved.");
    expect(screen.getByRole("status").className).toContain("mp-alert--success");
  });

  it("keeps stepper labels in the accessibility tree", () => {
    const { container } = render(
      <Stepper
        className="setup-progress"
        label="Setup progress"
        currentStep={2}
        steps={["Company details", "Payroll settings", "Review"]}
      />,
    );

    expect(screen.getByText("Step 2 of 3").getAttribute("aria-current")).toBe("step");
    expect(
      Array.from(container.querySelectorAll(".setup-progress__label")).map((node) => node.textContent),
    ).toEqual(["Company details", "Payroll settings", "Review"]);
  });

  it("raises an open select above the next field so options stay clickable", () => {
    const css = readFileSync(
      join(process.cwd(), "components", "ui", "form-controls.css"),
      "utf8",
    );
    const rule = css.match(/\.mp-field:has\(\[aria-expanded="true"\]\)\s*\{[^}]+\}/)?.[0];
    expect(rule).toMatch(/position:\s*relative/);
    expect(rule).toMatch(/z-index:\s*var\(--z-dropdown\)/);
  });
});
