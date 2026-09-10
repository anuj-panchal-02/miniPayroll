// @vitest-environment jsdom

import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { PayrollExtrasDrawer } from "./PayrollExtrasDrawer";

describe("PayrollExtrasDrawer", () => {
  afterEach(cleanup);

  it("does not render when closed", () => {
    render(
      <PayrollExtrasDrawer
        open={false}
        onClose={vi.fn()}
        employeeName="Ada Lovelace"
        employeeCode="EMP-01"
        initialOvertime={[]}
        initialBonuses={[]}
        initialDeductions={[]}
        onSave={vi.fn()}
      />,
    );

    expect(screen.queryByText("Ada Lovelace")).toBeNull();
  });

  it("renders employee details and calls onSave when Done is clicked", () => {
    const handleSave = vi.fn();
    const handleClose = vi.fn();

    // Mock HTMLDialogElement methods in jsdom
    HTMLDialogElement.prototype.showModal = vi.fn();
    HTMLDialogElement.prototype.close = vi.fn();

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

    fireEvent.click(screen.getByRole("button", { name: /^done$/i }));

    expect(handleSave).toHaveBeenCalledWith(
      [{ hours: "4", rate: "200", notes: "Weekend release" }],
      [],
      [],
    );
    expect(handleClose).toHaveBeenCalled();
  });
});
