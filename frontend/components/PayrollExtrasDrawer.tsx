"use client";

import { useEffect, useRef, useState, type MouseEvent } from "react";
import { Button } from "@/components/ui/Button";
import { FieldGroup } from "@/components/ui/FieldGroup";
import { Select } from "@/components/ui/Select";
import { BONUS_TYPE_OPTIONS, DEDUCTION_TYPE_OPTIONS } from "@/lib/payroll";

export type ExtraLine = { type: string; amount: string; notes: string };
export type OvertimeLine = { hours: string; rate: string; notes: string };

type PayrollExtrasDrawerProps = {
  open: boolean;
  onClose: () => void;
  employeeName: string;
  employeeCode: string;
  locked?: boolean;
  initialOvertime: OvertimeLine[];
  initialBonuses: ExtraLine[];
  initialDeductions: ExtraLine[];
  onSave: (overtime: OvertimeLine[], bonuses: ExtraLine[], deductions: ExtraLine[]) => void;
};

export function PayrollExtrasDrawer({
  open,
  onClose,
  employeeName,
  employeeCode,
  locked = false,
  initialOvertime,
  initialBonuses,
  initialDeductions,
  onSave,
}: PayrollExtrasDrawerProps) {
  const dialogRef = useRef<HTMLDialogElement>(null);
  const [overtime, setOvertime] = useState<OvertimeLine[]>(initialOvertime);
  const [bonuses, setBonuses] = useState<ExtraLine[]>(initialBonuses);
  const [deductions, setDeductions] = useState<ExtraLine[]>(initialDeductions);

  useEffect(() => {
    if (open) {
      setOvertime(initialOvertime);
      setBonuses(initialBonuses);
      setDeductions(initialDeductions);
      dialogRef.current?.showModal();
    } else {
      dialogRef.current?.close();
    }
  }, [open, initialOvertime, initialBonuses, initialDeductions]);

  function handleBackdropClick(event: MouseEvent<HTMLDialogElement>) {
    if (event.target === event.currentTarget) {
      handleDone();
    }
  }

  function handleDone() {
    onSave(overtime, bonuses, deductions);
    onClose();
  }

  if (!open) return null;

  return (
    <dialog
      ref={dialogRef}
      className="mp-drawer"
      onClick={handleBackdropClick}
      onCancel={(e) => {
        e.preventDefault();
        handleDone();
      }}
    >
      <div className="mp-drawer__inner">
        <header className="mp-drawer__head">
          <div>
            <h2 className="mp-drawer__title">{employeeName}</h2>
            <p className="mp-drawer__subtitle">
              ID: {employeeCode} · Monthly Adjustments
            </p>
          </div>
          <Button type="button" variant="ghost" onClick={handleDone}>
            ✕ Close
          </Button>
        </header>

        <div className="mp-drawer__body">
          {/* Overtime Section */}
          <FieldGroup
            title="Overtime"
            description="Hourly overtime tracking for this pay period."
          >
            {overtime.length === 0 ? (
              <p className="mp-group__hint">No overtime added.</p>
            ) : (
              overtime.map((item, index) => (
                <div className="sa-payroll-extra-row" key={`ot-${index}`}>
                  <input
                    className="sa-payroll-qty"
                    type="number"
                    step="0.25"
                    min="0"
                    placeholder="Hours"
                    aria-label="Overtime hours"
                    value={item.hours}
                    disabled={locked}
                    onChange={(e) => {
                      const next = [...overtime];
                      next[index] = { ...item, hours: e.target.value };
                      setOvertime(next);
                    }}
                  />
                  <input
                    className="sa-payroll-qty"
                    type="number"
                    step="0.01"
                    min="0"
                    placeholder="Rate (₹/hr)"
                    aria-label="Overtime hourly rate"
                    value={item.rate}
                    disabled={locked}
                    onChange={(e) => {
                      const next = [...overtime];
                      next[index] = { ...item, rate: e.target.value };
                      setOvertime(next);
                    }}
                  />
                  <input
                    className="mp-input"
                    placeholder="Notes (optional)"
                    aria-label="Overtime notes"
                    value={item.notes}
                    disabled={locked}
                    onChange={(e) => {
                      const next = [...overtime];
                      next[index] = { ...item, notes: e.target.value };
                      setOvertime(next);
                    }}
                  />
                  <Button
                    type="button"
                    variant="ghost"
                    disabled={locked}
                    onClick={() => setOvertime(overtime.filter((_, i) => i !== index))}
                  >
                    Remove
                  </Button>
                </div>
              ))
            )}
            <Button
              type="button"
              variant="secondary"
              disabled={locked}
              onClick={() =>
                setOvertime([...overtime, { hours: "", rate: "", notes: "" }])
              }
            >
              + Add overtime
            </Button>
          </FieldGroup>

          {/* Bonuses Section */}
          <FieldGroup
            title="Bonuses & Incentives"
            description="One-time taxable bonuses, festival, or performance pay."
          >
            {bonuses.length === 0 ? (
              <p className="mp-group__hint">No bonuses added.</p>
            ) : (
              bonuses.map((item, index) => (
                <div className="sa-payroll-extra-row" key={`bonus-${index}`}>
                  <Select
                    value={item.type}
                    options={BONUS_TYPE_OPTIONS}
                    disabled={locked}
                    onChange={(val) => {
                      const next = [...bonuses];
                      next[index] = { ...item, type: val };
                      setBonuses(next);
                    }}
                  />
                  <input
                    className="sa-payroll-qty"
                    type="number"
                    step="0.01"
                    min="0"
                    placeholder="Amount (₹)"
                    aria-label="Bonus amount"
                    value={item.amount}
                    disabled={locked}
                    onChange={(e) => {
                      const next = [...bonuses];
                      next[index] = { ...item, amount: e.target.value };
                      setBonuses(next);
                    }}
                  />
                  <input
                    className="mp-input"
                    placeholder="Notes (optional)"
                    aria-label="Bonus notes"
                    value={item.notes}
                    disabled={locked}
                    onChange={(e) => {
                      const next = [...bonuses];
                      next[index] = { ...item, notes: e.target.value };
                      setBonuses(next);
                    }}
                  />
                  <Button
                    type="button"
                    variant="ghost"
                    disabled={locked}
                    onClick={() => setBonuses(bonuses.filter((_, i) => i !== index))}
                  >
                    Remove
                  </Button>
                </div>
              ))
            )}
            <Button
              type="button"
              variant="secondary"
              disabled={locked}
              onClick={() =>
                setBonuses([...bonuses, { type: "0", amount: "", notes: "" }])
              }
            >
              + Add bonus
            </Button>
          </FieldGroup>

          {/* One-Time Deductions Section */}
          <FieldGroup
            title="One-Time Deductions"
            description="Advances, salary loans, or damage recovery for this month only."
          >
            {deductions.length === 0 ? (
              <p className="mp-group__hint">No deductions added.</p>
            ) : (
              deductions.map((item, index) => (
                <div className="sa-payroll-extra-row" key={`ded-${index}`}>
                  <Select
                    value={item.type}
                    options={DEDUCTION_TYPE_OPTIONS}
                    disabled={locked}
                    onChange={(val) => {
                      const next = [...deductions];
                      next[index] = { ...item, type: val };
                      setDeductions(next);
                    }}
                  />
                  <input
                    className="sa-payroll-qty"
                    type="number"
                    step="0.01"
                    min="0"
                    placeholder="Amount (₹)"
                    aria-label="Deduction amount"
                    value={item.amount}
                    disabled={locked}
                    onChange={(e) => {
                      const next = [...deductions];
                      next[index] = { ...item, amount: e.target.value };
                      setDeductions(next);
                    }}
                  />
                  <input
                    className="mp-input"
                    placeholder="Notes (optional)"
                    aria-label="Deduction notes"
                    value={item.notes}
                    disabled={locked}
                    onChange={(e) => {
                      const next = [...deductions];
                      next[index] = { ...item, notes: e.target.value };
                      setDeductions(next);
                    }}
                  />
                  <Button
                    type="button"
                    variant="ghost"
                    disabled={locked}
                    onClick={() => setDeductions(deductions.filter((_, i) => i !== index))}
                  >
                    Remove
                  </Button>
                </div>
              ))
            )}
            <Button
              type="button"
              variant="secondary"
              disabled={locked}
              onClick={() =>
                setDeductions([...deductions, { type: "0", amount: "", notes: "" }])
              }
            >
              + Add deduction
            </Button>
          </FieldGroup>
        </div>

        <footer className="mp-drawer__foot">
          <Button type="button" onClick={handleDone}>
            Done
          </Button>
        </footer>
      </div>
    </dialog>
  );
}
