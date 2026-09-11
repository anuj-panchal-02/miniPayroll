"use client";

import { FormEvent, useEffect, useRef, useState } from "react";
import { useToast } from "@/components/Toast";
import { Button } from "@/components/ui/Button";
import { Choice } from "@/components/ui/Choice";
import { Field } from "@/components/ui/Field";
import { Skeleton } from "@/components/ui/Skeleton";
import {
  defaultStatutoryPayrollValues,
  StatutoryPayrollFields,
  type StatutoryPayrollValues,
} from "@/components/StatutoryPayrollFields";
import {
  DailyRateMethod,
  getCompanySetup,
  updateCompletedPayrollSettings,
} from "@/lib/api";
import { weeklyOffDaysError, workingDaysError } from "@/lib/validation";
import "@/app/app/setup/setup.css";

const DAYS = [
  "Monday",
  "Tuesday",
  "Wednesday",
  "Thursday",
  "Friday",
  "Saturday",
  "Sunday",
];

export default function CompanySettingsPage() {
  const [workingDays, setWorkingDays] = useState("26");
  const [weeklyOffDays, setWeeklyOffDays] = useState<string[]>(["Sunday"]);
  const [dailyRateMethod, setDailyRateMethod] = useState<DailyRateMethod>(
    DailyRateMethod.CalendarDays,
  );
  const [statutory, setStatutory] = useState<StatutoryPayrollValues>(
    defaultStatutoryPayrollValues(),
  );
  const [companyState, setCompanyState] = useState<string | null>(null);
  const [workingDaysMessage, setWorkingDaysMessage] = useState("");
  const [weeklyOffMessage, setWeeklyOffMessage] = useState("");
  const [loadError, setLoadError] = useState("");
  const toast = useToast();
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const workingDaysRef = useRef<HTMLInputElement>(null);
  const weeklyOffRef = useRef<HTMLInputElement>(null);

  useEffect(() => {
    let cancelled = false;
    getCompanySetup()
      .then((setup) => {
        if (cancelled) return;
        setWorkingDays(String(setup.workingDaysPerMonth || 26));
        setWeeklyOffDays(setup.weeklyOffDays.length ? setup.weeklyOffDays : ["Sunday"]);
        setDailyRateMethod(
          setup.dailyRateMethod === "FixedThirty"
            ? DailyRateMethod.FixedThirty
            : DailyRateMethod.CalendarDays,
        );
        setCompanyState(setup.state);
        setStatutory(
          defaultStatutoryPayrollValues({
            pfApplicable: setup.pfApplicable,
            pfUseWageCeiling: setup.pfUseWageCeiling,
            esiApplicable: setup.esiApplicable,
            pfEstablishmentCode: setup.pfEstablishmentCode ?? "",
            esiCode: setup.esiCode ?? "",
          }),
        );
      })
      .catch((reason) => {
        if (!cancelled) {
          setLoadError(reason instanceof Error ? reason.message : "Unable to load settings.");
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, []);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const nextWorkingDaysError = workingDaysError(workingDays);
    const nextWeeklyOffError = weeklyOffDaysError(weeklyOffDays);
    setWorkingDaysMessage(nextWorkingDaysError ?? "");
    setWeeklyOffMessage(nextWeeklyOffError ?? "");
    toast.dismiss();

    if (nextWorkingDaysError) {
      workingDaysRef.current?.focus();
      return;
    }
    if (nextWeeklyOffError) {
      weeklyOffRef.current?.focus();
      return;
    }

    setSaving(true);
    try {
      await updateCompletedPayrollSettings({
        dailyRateMethod,
        workingDaysPerMonth: Number(workingDays),
        weeklyOffDays,
        pfApplicable: statutory.pfApplicable,
        pfUseWageCeiling: statutory.pfUseWageCeiling,
        esiApplicable: statutory.esiApplicable,
        pfEstablishmentCode: statutory.pfEstablishmentCode.trim() || null,
        esiCode: statutory.esiCode.trim() || null,
      });
      toast.showSuccess("Payroll settings saved.");
    } catch (reason) {
      toast.showError(reason instanceof Error ? reason.message : "Unable to save settings.");
    } finally {
      setSaving(false);
    }
  }

  return (
    <main className="sa-shell">
      <header className="sa-head">
        <h1>Settings</h1>
        <p>Daily rate, weekly offs, and statutory deduction policy for this company.</p>
      </header>
      {loading ? (
        <div className="space-y-3" role="status">
          <Skeleton className="h-11 w-full" />
          <Skeleton className="h-11 w-full" />
          <Skeleton className="h-24 w-full" />
        </div>
      ) : null}
      {loadError ? <p className="setup-alert" role="alert">{loadError}</p> : null}
      {!loading && !loadError ? (
        <form className="setup-form" noValidate autoComplete="off" onSubmit={submit}>
          <Field
            id="working-days"
            label="Working days per month"
            error={workingDaysMessage || null}
          >
            <input
              ref={workingDaysRef}
              className="mp-input"
              type="number"
              min="1"
              max="31"
              step="1"
              inputMode="numeric"
              value={workingDays}
              onChange={(event) => {
                setWorkingDays(event.target.value);
                setWorkingDaysMessage("");
              }}
            />
          </Field>
          <fieldset
            className="setup-fieldset"
            aria-invalid={Boolean(weeklyOffMessage)}
            aria-describedby={weeklyOffMessage ? "weekly-off-error" : undefined}
          >
            <legend>Weekly off days</legend>
            <div className="setup-check-grid mp-choice-grid">
              {DAYS.map((day, index) => (
                <Choice
                  key={day}
                  className="setup-check"
                  type="checkbox"
                  value={day}
                  checked={weeklyOffDays.includes(day)}
                  inputRef={index === 0 ? weeklyOffRef : undefined}
                  label={day}
                  onChange={(event) => {
                    setWeeklyOffDays((current) =>
                      event.target.checked
                        ? DAYS.filter((candidate) => current.includes(candidate) || candidate === day)
                        : current.filter((candidate) => candidate !== day),
                    );
                    setWeeklyOffMessage("");
                  }}
                />
              ))}
            </div>
            {weeklyOffMessage ? (
              <p id="weekly-off-error" className="setup-field__error">
                {weeklyOffMessage}
              </p>
            ) : null}
          </fieldset>
          <fieldset className="setup-fieldset">
            <legend>Daily rate method</legend>
            <div className="setup-rate-options">
              <Choice
                className="setup-rate"
                type="radio"
                name="dailyRateMethod"
                checked={dailyRateMethod === DailyRateMethod.CalendarDays}
                onChange={() => setDailyRateMethod(DailyRateMethod.CalendarDays)}
                label={
                  <span>
                    <strong>Calendar days</strong>
                    <small>Monthly salary ÷ calendar days in that month.</small>
                  </span>
                }
              />
              <Choice
                className="setup-rate"
                type="radio"
                name="dailyRateMethod"
                checked={dailyRateMethod === DailyRateMethod.FixedThirty}
                onChange={() => setDailyRateMethod(DailyRateMethod.FixedThirty)}
                label={
                  <span>
                    <strong>Fixed 30 days</strong>
                    <small>Monthly salary ÷ 30.</small>
                  </span>
                }
              />
            </div>
          </fieldset>
          <StatutoryPayrollFields
            values={statutory}
            companyState={companyState}
            onChange={setStatutory}
          />
          <div className="setup-actions">
            <Button type="submit" loading={saving} loadingLabel="Saving…">
              Save settings
            </Button>
          </div>
        </form>
      ) : null}
    </main>
  );
}
