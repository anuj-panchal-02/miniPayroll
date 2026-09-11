// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { PayrollExtrasDrawer } from "./PayrollExtrasDrawer";

const baseProps = {
  employeeName: "Ada Lovelace",
  employeeCode: "EMP-01",
  initialOvertime: [] as { hours: string; rate: string; notes: string }[],
  initialBonuses: [] as { type: string; amount: string; notes: string }[],
  initialDeductions: [] as { type: string; amount: string; notes: string }[],
};

describe("PayrollExtrasDrawer", () => {
  afterEach(cleanup);

  beforeEach(() => {
    HTMLDialogElement.prototype.showModal = function showModal(this: HTMLDialogElement) {
      this.setAttribute("open", "");
    };
    HTMLDialogElement.prototype.close = function close(this: HTMLDialogElement) {
      this.removeAttribute("open");
    };
  });

  it("stays mounted when closed", () => {
    render(
      <PayrollExtrasDrawer
        open={false}
        onClose={vi.fn()}
        onSave={vi.fn()}
        {...baseProps}
      />,
    );

    const dialog = document.querySelector("dialog");
    expect(dialog).toBeTruthy();
    expect(dialog?.open).toBe(false);
  });

  it("renders employee details and calls onSave when Done is clicked", () => {
    const handleSave = vi.fn();
    const handleClose = vi.fn();

    render(
      <PayrollExtrasDrawer
        open={true}
        onClose={handleClose}
        employeeName="Ada Lovelace"
        employeeCode="EMP-01"
        initialOvertime={[{ hours: "4", rate: "200", notes: "Weekend release" }]}
        initialBonuses={[]}
        initialDeductions={[]}
        onSave={handleSave}
      />,
    );

    expect(screen.getByText("Ada Lovelace")).toBeDefined();
    expect(screen.getByText(/EMP-01/)).toBeDefined();
    expect(screen.getByLabelText("Hours")).toBeTruthy();
    expect(screen.getByLabelText("Hours")).toHaveProperty("value", "4");
    expect(screen.getByLabelText("Rate")).toHaveProperty("value", "200");

    fireEvent.click(screen.getByRole("button", { name: /^done$/i }));

    expect(handleSave).toHaveBeenCalledWith(
      [{ hours: "4", rate: "200", notes: "Weekend release" }],
      [],
      [],
    );
    expect(handleClose).toHaveBeenCalled();
  });

  it("discards edits when Close or Cancel is clicked", () => {
    const handleSave = vi.fn();
    const handleClose = vi.fn();

    render(
      <PayrollExtrasDrawer
        open={true}
        onClose={handleClose}
        employeeName="Ada Lovelace"
        employeeCode="EMP-01"
        initialOvertime={[{ hours: "4", rate: "200", notes: "" }]}
        initialBonuses={[{ type: "0", amount: "500", notes: "" }]}
        initialDeductions={[]}
        onSave={handleSave}
      />,
    );

    expect(screen.getByLabelText("Amount")).toBeTruthy();
    fireEvent.click(screen.getByRole("button", { name: /^close$/i }));
    expect(handleSave).not.toHaveBeenCalled();
    expect(handleClose).toHaveBeenCalledTimes(1);

    fireEvent.click(screen.getByRole("button", { name: /^cancel$/i }));
    expect(handleSave).not.toHaveBeenCalled();
    expect(handleClose).toHaveBeenCalledTimes(2);
  });
});
