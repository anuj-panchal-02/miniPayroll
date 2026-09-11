"use client";

import { FormEvent, useEffect, useState } from "react";
import { useToast } from "@/components/Toast";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Field } from "@/components/ui/Field";
import { FieldGroup } from "@/components/ui/FieldGroup";
import { ListPager, usePager } from "@/components/ui/ListPager";
import { Select } from "@/components/ui/Select";
import { Skeleton } from "@/components/ui/Skeleton";
import { pageSlice } from "@/lib/paging";
import {
  createPlatformCity,
  listPlatformCities,
  listPlatformStates,
  updatePlatformCity,
  type PlatformCityItem,
  type PlatformStateItem,
} from "@/lib/api";

export default function PlatformCitiesPage() {
  const [states, setStates] = useState<PlatformStateItem[]>([]);
  const [cities, setCities] = useState<PlatformCityItem[]>([]);
  const [stateId, setStateId] = useState("");
  const [name, setName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const toast = useToast();
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editName, setEditName] = useState("");

  useEffect(() => {
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
  }, []);

  useEffect(() => {
    if (!stateId) {
      setCities([]);
      return;
    }

    let cancelled = false;
    listPlatformCities(stateId, true)
      .then((items) => {
        if (!cancelled) {
          setCities(items);
          setError(null);
        }
      })
      .catch((reason) => {
        if (!cancelled) {
          setError(reason instanceof Error ? reason.message : "Could not load cities.");
        }
      });
    return () => {
      cancelled = true;
    };
  }, [stateId]);

  async function onAdd(event: FormEvent) {
    event.preventDefault();
    if (!stateId) return;
    setBusy(true);
    try {
      const created = await createPlatformCity({ stateId, name });
      setCities((current) => [...current, created].sort((a, b) => a.sortOrder - b.sortOrder));
      setName("");
      toast.showSuccess("City added.");
    } catch (reason) {
      toast.showError(reason instanceof Error ? reason.message : "Could not add the city.");
    } finally {
      setBusy(false);
    }
  }

  async function onRename(id: string) {
    setBusy(true);
    try {
      const updated = await updatePlatformCity(id, { name: editName });
      setCities((current) => current.map((item) => (item.id === id ? updated : item)));
      setEditingId(null);
      toast.showSuccess("City saved.");
    } catch (reason) {
      toast.showError(reason instanceof Error ? reason.message : "Could not rename the city.");
    } finally {
      setBusy(false);
    }
  }

  async function onToggle(city: PlatformCityItem) {
    setBusy(true);
    try {
      const updated = await updatePlatformCity(city.id, { isActive: !city.isActive });
      setCities((current) => current.map((item) => (item.id === city.id ? updated : item)));
      toast.showSuccess("City saved.");
    } catch (reason) {
      toast.showError(reason instanceof Error ? reason.message : "Could not update the city.");
    } finally {
      setBusy(false);
    }
  }

  const pager = usePager(cities.length, stateId);
  const visible = pageSlice(cities, pager.page, pager.pageSize);

  return (
    <main className="sa-shell">
        <header className="sa-head">
          <h1>Cities</h1>
          <p>Cities belong to a state and appear on company and employee address forms.</p>
        </header>

        <Alert>{error}</Alert>

        <form className="sa-compose" onSubmit={onAdd}>
          <FieldGroup title="Add a city" className="mp-group--inline mp-group--inline-fields">
            <Field id="city-state" label="State">
              <Select
                value={stateId}
                options={states.map((state) => ({
                  value: state.id,
                  label: state.name,
                }))}
                onChange={(next) => {
                  setStateId(next);
                  setEditingId(null);
                }}
              />
            </Field>
            <Field id="city-name" label="Name">
              <input
                className="mp-input"
                value={name}
                disabled={!stateId}
                onChange={(event) => setName(event.target.value)}
              />
            </Field>
            <Button type="submit" loading={busy} loadingLabel="Adding…" disabled={!stateId}>
              Add city
            </Button>
          </FieldGroup>
        </form>

        {loading ? (
          <div className="sa-master-wrap" role="status">
            <Skeleton className="h-12 w-full" />
            <Skeleton className="mt-2 h-14 w-full" />
            <Skeleton className="mt-2 h-14 w-full" />
          </div>
        ) : !stateId ? (
          <p className="sa-empty">Select a state to manage its cities.</p>
        ) : (
          <>
          <div className="sa-master-wrap">
            <table className="sa-master">
              <thead>
                <tr>
                  <th scope="col">Name</th>
                  <th scope="col">Status</th>
                  <th scope="col">Actions</th>
                </tr>
              </thead>
              <tbody>
                {visible.map((city) =>
                  editingId === city.id ? (
                    <tr key={city.id}>
                      <td colSpan={3}>
                        <form
                          className="sa-master__edit sa-master__edit--city"
                          onSubmit={(event) => {
                            event.preventDefault();
                            void onRename(city.id);
                          }}
                        >
                          <Field id={`edit-city-${city.id}`} label="Name">
                            <input
                              className="mp-input"
                              value={editName}
                              onChange={(event) => setEditName(event.target.value)}
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
                    <tr key={city.id}>
                      <td>{city.name}</td>
                      <td>
                        <span className="sa-chip" data-status={city.isActive ? "Active" : "Pending"}>
                          {city.isActive ? "Active" : "Inactive"}
                        </span>
                      </td>
                      <td>
                        <div className="sa-master__actions">
                          <Button
                            type="button"
                            variant="ghost"
                            onClick={() => {
                              setEditingId(city.id);
                              setEditName(city.name);
                            }}
                          >
                            Rename
                          </Button>
                          <Button
                            type="button"
                            variant="ghost"
                            disabled={busy}
                            onClick={() => void onToggle(city)}
                          >
                            {city.isActive ? "Deactivate" : "Reactivate"}
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
            id="cities"
            page={pager.page}
            pageSize={pager.pageSize}
            total={cities.length}
            onPageChange={pager.setPage}
            onPageSizeChange={pager.setPageSize}
          />
          </>
        )}
    </main>
  );
}
