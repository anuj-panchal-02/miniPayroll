"use client";

import { FormEvent, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { SuperadminShell } from "@/components/SuperadminShell";
import { ToastOutlet, useToast } from "@/components/Toast";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Field } from "@/components/ui/Field";
import { FieldGroup } from "@/components/ui/FieldGroup";
import { ListPager, usePager } from "@/components/ui/ListPager";
import { pageSlice } from "@/lib/paging";
import {
  createPlatformState,
  getToken,
  listPlatformStates,
  updatePlatformState,
  type PlatformStateItem,
} from "@/lib/api";

export default function PlatformStatesPage() {
  const router = useRouter();
  const [states, setStates] = useState<PlatformStateItem[]>([]);
  const [name, setName] = useState("");
  const [code, setCode] = useState("");
  const [error, setError] = useState<string | null>(null);
  const toast = useToast();
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editName, setEditName] = useState("");
  const [editCode, setEditCode] = useState("");

  useEffect(() => {
    if (!getToken()) {
      router.replace("/login");
      return;
    }

    let cancelled = false;
    listPlatformStates(true)
      .then((items) => {
        if (!cancelled) {
          setStates(items);
          setError(null);
        }
      })
      .catch((reason) => {
        if (!cancelled) {
          setError(reason instanceof Error ? reason.message : "Could not load states.");
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [router.replace]);

  async function onAdd(event: FormEvent) {
    event.preventDefault();
    setBusy(true);
    try {
      const created = await createPlatformState({ name, code });
      setStates((current) => [...current, created].sort((a, b) => a.sortOrder - b.sortOrder));
      setName("");
      setCode("");
      toast.showSuccess("State added.");
    } catch (reason) {
      toast.showError(reason instanceof Error ? reason.message : "Could not add the state.");
    } finally {
      setBusy(false);
    }
  }

  async function onRename(id: string) {
    setBusy(true);
    try {
      const updated = await updatePlatformState(id, { name: editName, code: editCode });
      setStates((current) => current.map((item) => (item.id === id ? updated : item)));
      setEditingId(null);
      toast.showSuccess("State saved.");
    } catch (reason) {
      toast.showError(reason instanceof Error ? reason.message : "Could not rename the state.");
    } finally {
      setBusy(false);
    }
  }

  async function onToggle(state: PlatformStateItem) {
    setBusy(true);
    try {
      const updated = await updatePlatformState(state.id, { isActive: !state.isActive });
      setStates((current) => current.map((item) => (item.id === state.id ? updated : item)));
      toast.showSuccess("State saved.");
    } catch (reason) {
      toast.showError(reason instanceof Error ? reason.message : "Could not update the state.");
    } finally {
      setBusy(false);
    }
  }

  const pager = usePager(states.length);
  const visible = pageSlice(states, pager.page, pager.pageSize);

  return (
    <SuperadminShell>
      <main className="sa-shell">
        <header className="sa-head">
          <h1>States</h1>
          <p>Canonical Indian states and union territories used on company and employee forms.</p>
        </header>

        <Alert>{error}</Alert>
        <ToastOutlet toast={toast} />

        <form className="sa-compose" onSubmit={onAdd}>
          <FieldGroup title="Add a state" className="mp-group--inline">
            <Field id="state-name" label="Name">
              <input
                className="mp-input"
                value={name}
                onChange={(event) => setName(event.target.value)}
              />
            </Field>
            <Field id="state-code" label="Code">
              <input
                className="mp-input"
                value={code}
                onChange={(event) => setCode(event.target.value)}
              />
            </Field>
            <Button type="submit" loading={busy} loadingLabel="Adding…">
              Add state
            </Button>
          </FieldGroup>
        </form>

        {loading ? (
          <p className="sa-empty" role="status">
            Loading states.
          </p>
        ) : (
          <>
          <div className="sa-master-wrap">
            <table className="sa-master">
              <thead>
                <tr>
                  <th scope="col">Name</th>
                  <th scope="col" className="sa-master__code">
                    Code
                  </th>
                  <th scope="col">Status</th>
                  <th scope="col">Actions</th>
                </tr>
              </thead>
              <tbody>
                {visible.map((state) =>
                  editingId === state.id ? (
                    <tr key={state.id}>
                      <td colSpan={4}>
                        <form
                          className="sa-master__edit"
                          onSubmit={(event) => {
                            event.preventDefault();
                            void onRename(state.id);
                          }}
                        >
                          <Field id={`edit-name-${state.id}`} label="Name">
                            <input
                              className="mp-input"
                              value={editName}
                              onChange={(event) => setEditName(event.target.value)}
                            />
                          </Field>
                          <Field id={`edit-code-${state.id}`} label="Code">
                            <input
                              className="mp-input"
                              value={editCode}
                              onChange={(event) => setEditCode(event.target.value)}
                            />
                          </Field>
                          <div className="sa-master__actions">
                            <Button type="submit" loading={busy}>
                              Save
                            </Button>
                            <Button
                              type="button"
                              variant="secondary"
                              onClick={() => setEditingId(null)}
                            >
                              Cancel
                            </Button>
                          </div>
                        </form>
                      </td>
                    </tr>
                  ) : (
                    <tr key={state.id}>
                      <td>{state.name}</td>
                      <td className="sa-master__code">{state.code}</td>
                      <td>
                        <span className="sa-chip" data-status={state.isActive ? "Active" : "Pending"}>
                          {state.isActive ? "Active" : "Inactive"}
                        </span>
                      </td>
                      <td>
                        <div className="sa-master__actions">
                          <Button
                            type="button"
                            variant="ghost"
                            onClick={() => {
                              setEditingId(state.id);
                              setEditName(state.name);
                              setEditCode(state.code);
                            }}
                          >
                            Rename
                          </Button>
                          <Button
                            type="button"
                            variant="ghost"
                            disabled={busy}
                            onClick={() => void onToggle(state)}
                          >
                            {state.isActive ? "Deactivate" : "Reactivate"}
                          </Button>
                        </div>
                      </td>
                    </tr>
                  ),
                )}
              </tbody>
            </table>
          </div>
          <ListPager
            id="states"
            page={pager.page}
            pageSize={pager.pageSize}
            total={states.length}
            onPageChange={pager.setPage}
            onPageSizeChange={pager.setPageSize}
          />
          </>
        )}
      </main>
    </SuperadminShell>
  );
}
