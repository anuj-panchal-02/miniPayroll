"use client";

import { useEffect, useState } from "react";
import {
  SalaryStructureEditor,
  salaryStructureError,
  salaryStructureFieldsFrom,
  toSalaryStructureInput,
  type SalaryStructureFields,
} from "@/components/SalaryStructureEditor";
import {
  createSalaryStructure,
  listSalaryStructures,
  SalaryComponentValueType,
  type EmployeeDetail,
  type SalaryStructureComponentDetail,
  type SalaryStructureDetail,
} from "@/lib/api";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";

export function SalaryStructurePanel({ employee }: { employee: EmployeeDetail }) {
  const [structures, setStructures] = useState<SalaryStructureDetail[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [editor, setEditor] = useState<SalaryStructureFields | null>(null);
  const [editorError, setEditorError] = useState("");
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let cancelled = false;
    void listSalaryStructures(employee.id)
      .then((items) => {
        if (!cancelled) setStructures(items);
      })
      .catch((reason) => {
        if (!cancelled) setError(reason instanceof Error ? reason.message : "Could not load salary structures.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [employee.id]);

  function startRevision() {
    const current = structures[0];
    setEditor(
      current
        ? salaryStructureFieldsFrom({
            effectiveFrom: today(),
            components: current.components,
          })
        : {
            effectiveFrom: employee.joiningDate ?? today(),
            components: [
              {
                key: "basic",
                name: "Basic Salary",
                type: 0,
                valueType: 0,
                value: "",
              },
            ],
          },
    );
    setEditorError("");
  }

  async function saveRevision() {
    if (!editor) return;
    const message = salaryStructureError(editor, employee.joiningDate ?? "");
    if (message) {
      setEditorError(message);
      return;
    }
    setSaving(true);
    setEditorError("");
    try {
      const created = await createSalaryStructure(employee.id, toSalaryStructureInput(editor));
      setStructures((current) => [created, ...current]);
      setEditor(null);
    } catch (reason) {
      setEditorError(reason instanceof Error ? reason.message : "Could not save the salary revision.");
    } finally {
      setSaving(false);
    }
  }

  const current = structures[0];
  const previous = structures.slice(1);

  return (
    <section className="sa-salary-history" aria-labelledby="salary-history-title">
      <header className="sa-salary-history__intro">
        <div className="sa-salary-history__copy">
          <h2 id="salary-history-title">Salary structure</h2>
          <p className="sa-muted">
            Salary changes are saved as dated versions and do not overwrite history.
          </p>
        </div>
        {editor || loading || error ? null : (
          <Button type="button" onClick={startRevision}>
            {structures.length ? "Add salary revision" : "Add salary structure"}
          </Button>
        )}
      </header>
      <Alert>{error || null}</Alert>
      {loading ? (
        <p className="sa-salary-history__status" role="status">
          Loading salary history…
        </p>
      ) : null}
      {!loading && !error && structures.length === 0 && !editor ? (
        <p className="sa-salary-history__status" role="status">
          No salary structure yet. Add one before this employee is included in payroll.
        </p>
      ) : null}
      {current ? (
        <SalaryLedger structure={current} current />
      ) : null}
      {previous.map((structure) => (
        <SalaryLedger key={structure.id} structure={structure} />
      ))}
      {editor ? (
        <div className="sa-salary-history__editor">
          <SalaryStructureEditor
            value={editor}
            joiningDate={employee.joiningDate ?? ""}
            error={editorError}
            onChange={(next) => {
              setEditor(next);
              setEditorError("");
            }}
          />
          <div className="sa-compose__actions">
            <Button
              type="button"
              loading={saving}
              loadingLabel="Saving…"
              onClick={() => void saveRevision()}
            >
              Save salary revision
            </Button>
            <Button type="button" variant="secondary" disabled={saving} onClick={() => setEditor(null)}>
              Cancel
            </Button>
          </div>
        </div>
      ) : null}
    </section>
  );
}

function SalaryLedger({
  structure,
  current = false,
}: {
  structure: SalaryStructureDetail;
  current?: boolean;
}) {
  const titleId = current ? "current-structure-title" : `previous-structure-${structure.id}`;
  return (
    <article
      className={current ? "sa-salary-ledger" : "sa-salary-ledger sa-salary-ledger--past"}
      aria-labelledby={titleId}
    >
      <header className="sa-salary-ledger__head">
        <h3 id={titleId}>{current ? "Current structure" : "Previous structure"}</h3>
        <p>Effective {structure.effectiveFrom}</p>
      </header>
      <dl className="sa-salary-ledger__totals">
        <div>
          <dt>Earnings</dt>
          <dd>{rupees(structure.recurringEarnings)}</dd>
        </div>
        <div>
          <dt>Deductions</dt>
          <dd>{rupees(structure.recurringDeductions)}</dd>
        </div>
      </dl>
      <ul className="sa-salary-ledger__lines">
        {structure.components.map((component) => (
          <li key={component.id}>
            <span>{component.name}</span>
            <span>{formatComponentValue(component)}</span>
          </li>
        ))}
      </ul>
    </article>
  );
}

function rupees(value: number): string {
  return `₹${value.toLocaleString("en-IN")}`;
}

function formatComponentValue(component: SalaryStructureComponentDetail): string {
  if (component.valueType === SalaryComponentValueType.PercentageOfBasic) {
    return `${component.value.toLocaleString("en-IN")}% of Basic`;
  }
  return rupees(component.value);
}

function today(): string {
  return new Date().toISOString().slice(0, 10);
}
