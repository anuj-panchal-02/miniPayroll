"use client";

import { FormEvent, useEffect, useState } from "react";
import { useToast } from "@/components/Toast";
import { Alert } from "@/components/ui/Alert";
import { Button } from "@/components/ui/Button";
import { Field } from "@/components/ui/Field";
import { FieldGroup } from "@/components/ui/FieldGroup";
import { Skeleton } from "@/components/ui/Skeleton";
import {
  getPlatformPlan,
  updatePlatformPlan,
  type PlatformPlan,
} from "@/lib/api";

export default function PlanSettingsPage() {
  const toast = useToast();
  const [plan, setPlan] = useState<PlatformPlan | null>(null);
  const [price, setPrice] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let cancelled = false;
    getPlatformPlan()
      .then((loaded) => {
        if (cancelled) {
          return;
        }
        setPlan(loaded);
        setPrice(String(loaded.pricePerEmployee));
        setError(null);
      })
      .catch((reason) => {
        if (!cancelled) {
          setError(reason instanceof Error ? reason.message : "Could not load the plan.");
        }
      })
      .finally(() => {
        if (!cancelled) {
          setLoading(false);
        }
      });
    return () => {
      cancelled = true;
    };
  }, []);

  async function onSave(event: FormEvent) {
    event.preventDefault();
    const next = Number(price);
    if (!Number.isFinite(next) || next <= 0) {
      toast.showError("Enter a price greater than zero.");
      return;
    }
    setBusy(true);
    toast.dismiss();
    try {
      const updated = await updatePlatformPlan(next);
      setPlan(updated);
      setPrice(String(updated.pricePerEmployee));
      toast.showSuccess("Plan price saved.");
    } catch (reason) {
      toast.showError(reason instanceof Error ? reason.message : "Could not save the plan.");
    } finally {
      setBusy(false);
    }
  }

  return (
    <main className="sa-shell">
        <header className="sa-head">
          <h1>Plan</h1>
          <p>
            Basic is the only public plan. The per-employee price applies to the open
            month. Closed months keep the price already billed.
          </p>
        </header>

        <Alert>{error}</Alert>
        {loading ? (
          <div className="space-y-3 py-2" role="status">
            <Skeleton className="h-10 w-full max-w-sm" />
            <Skeleton className="h-11 w-32" />
          </div>
        ) : (
          <form className="sa-compose" noValidate onSubmit={onSave}>
            <FieldGroup title={plan?.name ?? "Basic"} className="mp-group--inline-2">
              <Field
                id="price-per-employee"
                label="Price per employee"
                hint="Rupees per billable employee each calendar month. Closed months keep the billed price; the first load after this change backfills any missing closed months at today's price, once."
                required
              >
                <input
                  className="mp-input"
                  name="pricePerEmployee"
                  type="number"
                  min="0.01"
                  step="0.01"
                  value={price}
                  onChange={(event) => {
                    setPrice(event.target.value);
                  }}
                  disabled={busy}
                />
              </Field>
              <Button type="submit" loading={busy} loadingLabel="Saving">
                Save price
              </Button>
            </FieldGroup>
          </form>
        )}
    </main>
  );
}
