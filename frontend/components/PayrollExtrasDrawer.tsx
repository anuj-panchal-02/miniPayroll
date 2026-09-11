"use client";

import { useEffect, useRef, useState, type MouseEvent } from "react";
import { Button } from "@/components/ui/Button";
import { Field } from "@/components/ui/Field";
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
      dialogRef.current?.showModal?.();
    } else {
      dialogRef.current?.close?.();
    }
  }, [open, initialOvertime, initialBonuses, initialDeductions]);

  function handleDiscard() {
    onClose();
  }

  function handleBackdropClick(event: MouseEvent<HTMLDialogElement>) {
    if (event.target === event.currentTarget) {
      handleDiscard();
    }
  }

  function handleDone() {
    onSave(overtime, bonuses, deductions);
    onClose();
  }

  return (
    <dialog
      ref={dialogRef}
      className="mp-drawer"
      hidden={!open}
      onClick={handleBackdropClick}
      onCancel={(e) => {
        e.preventDefault();
        handleDiscard();
      }}
    >
      <div className="mp-drawer__inner">
        <header className="mp-drawer__head">
          <div>
            <h2 className="mp-drawer__title">{employeeName}</h2>
            <p className="mp-drawer__subtitle">
              {employeeCode} · Monthly adjustments
            </p>
          </div>
          <Button type="button" variant="ghost" onClick={handleDiscard}>
            Close
          </Button>
        </header>

        <div className="mp-drawer__body">
          <div className="mp-drawer__facts" role="region" aria-label="Adjustment counts">
            <div className="mp-drawer__fact">
              <span className="mp-kpi-card__label">Overtime</span>
              <span className="mp-kpi-card__value">{overtime.length}</span>
            </div>
            <div className="mp-drawer__fact">
              <span className="mp-kpi-card__label">Bonuses</span>
              <span className="mp-kpi-card__value">{bonuses.length}</span>
            </div>
            <div className="mp-drawer__fact">
              <span className="mp-kpi-card__label">Deductions</span>
              <span className="mp-kpi-card__value">{deductions.length}</span>
            </div>
          </div>

          <FieldGroup
            title="Overtime"
            hint="Hourly overtime tracking for this pay period."
          >
            {overtime.length === 0 ? (
              <p className="mp-group__hint">No overtime added.</p>
            ) : (
              overtime.map((item, index) => (
                <div className="sa-payroll-extra-row" key={`ot-${index}`}>
                  <Field id={`ot-${index}-hours`} label="Hours">
                    <input
                      className="mp-input"
                      type="number"
                      step="0.25"
                      min="0"
                      value={item.hours}
                      disabled={locked}
                      onChange={(e) => {
                        const next = [...overtime];
                        next[index] = { ...item, hours: e.target.value };
                        setOvertime(next);
                      }}
                    />
                  </Field>
                  <Field id={`ot-${index}-rate`} label="Rate">
                    <input
                      className="mp-input"
                      type="number"
                      step="0.01"
                      min="0"
                      value={item.rate}
                      disabled={locked}
                      onChange={(e) => {
                        const next = [...overtime];
                        next[index] = { ...item, rate: e.target.value };
                        setOvertime(next);
                      }}
                    />
                  </Field>
                  <Field id={`ot-${index}-notes`} label="Notes" optional>
                    <input
                      className="mp-input"
                      value={item.notes}
                      disabled={locked}
                      onChange={(e) => {
                        const next = [...overtime];
                        next[index] = { ...item, notes: e.target.value };
                        setOvertime(next);
                      }}
                    />
                  </Field>
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
              Add overtime
            </Button>
          </FieldGroup>

          <FieldGroup
            title="Bonuses & Incentives"
            hint="One-time taxable bonuses, festival, or performance pay."
          >
            {bonuses.length === 0 ? (
              <p className="mp-group__hint">No bonuses added.</p>
            ) : (
              bonuses.map((item, index) => (
                <div className="sa-payroll-extra-row sa-payroll-extra-row--typed" key={`bonus-${index}`}>
                  <Field id={`bonus-${index}-type`} label="Type">
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
                  </Field>
                  <Field id={`bonus-${index}-amount`} label="Amount">
                    <input
                      className="mp-input"
                      type="number"
                      step="0.01"
                      min="0"
                      value={item.amount}
                      disabled={locked}
                      onChange={(e) => {
                        const next = [...bonuses];
                        next[index] = { ...item, amount: e.target.value };
                        setBonuses(next);
                      }}
                    />
                  </Field>
                  <Field id={`bonus-${index}-notes`} label="Notes" optional>
                    <input
                      className="mp-input"
                      value={item.notes}
                      disabled={locked}
                      onChange={(e) => {
                        const next = [...bonuses];
                        next[index] = { ...item, notes: e.target.value };
                        setBonuses(next);
                      }}
                    />
                  </Field>
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
              Add bonus
            </Button>
          </FieldGroup>

          <FieldGroup
            title="One-Time Deductions"
            hint="Advances, salary loans, or damage recovery for this month only."
          >
            {deductions.length === 0 ? (
              <p className="mp-group__hint">No deductions added.</p>
            ) : (
              deductions.map((item, index) => (
                <div className="sa-payroll-extra-row sa-payroll-extra-row--typed" key={`ded-${index}`}>
                  <Field id={`ded-${index}-type`} label="Type">
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
                  </Field>
                  <Field id={`ded-${index}-amount`} label="Amount">
                    <input
                      className="mp-input"
                      type="number"
                      step="0.01"
                      min="0"
                      value={item.amount}
                      disabled={locked}
                      onChange={(e) => {
                        const next = [...deductions];
                        next[index] = { ...item, amount: e.target.value };
                        setDeductions(next);
                      }}
                    />
                  </Field>
                  <Field id={`ded-${index}-notes`} label="Notes" optional>
                    <input
                      className="mp-input"
                      value={item.notes}
                      disabled={locked}
                      onChange={(e) => {
                        const next = [...deductions];
                        next[index] = { ...item, notes: e.target.value };
                        setDeductions(next);
                      }}
                    />
                  </Field>
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
              Add deduction
            </Button>
          </FieldGroup>
        </div>

        <footer className="mp-drawer__foot">
          <Button type="button" variant="secondary" onClick={handleDiscard}>
            Cancel
          </Button>
          <Button type="button" onClick={handleDone}>
            Done
          </Button>
        </footer>
      </div>
    </dialog>
  );
}
