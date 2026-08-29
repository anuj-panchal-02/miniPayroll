"use client";

import { FormEvent, useEffect, useRef, useState } from "react";
import { useRouter } from "next/navigation";
import { SetupWizardShell } from "@/components/SetupWizardShell";
import {
  DailyRateMethod,
  getCompanySetup,
  updatePayrollSettings,
} from "@/lib/api";
import { weeklyOffDaysError, workingDaysError } from "@/lib/validation";

const DAYS = [
  "Monday",
  "Tuesday",
  "Wednesday",
  "Thursday",
  "Friday",
  "Saturday",
  "Sunday",
];

export default function PayrollSetupPage() {
  const router = useRouter();
  const [workingDays, setWorkingDays] = useState("26");
  const [weeklyOffDays, setWeeklyOffDays] = useState<string[]>(["Sunday"]);
  const [dailyRateMethod, setDailyRateMethod] = useState<DailyRateMethod>(
    DailyRateMethod.CalendarDays,
  );
  const [workingDaysMessage, setWorkingDaysMessage] = useState("");
  const [weeklyOffMessage, setWeeklyOffMessage] = useState("");
  const [loadError, setLoadError] = useState("");
  const [submitError, setSubmitError] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const workingDaysRef = useRef<HTMLInputElement>(null);
  const weeklyOffRef = useRef<HTMLInputElement>(null);
  const mountedRef = useRef(false);
  const submissionRef = useRef(0);

  useEffect(() => {
    mountedRef.current = true;
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
      })
      .catch((reason) => {
        if (!cancelled) {
          setLoadError(reason instanceof Error ? reason.message : "Unable to load payroll settings.");
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
      mountedRef.current = false;
      submissionRef.current += 1;
    };
  }, []);

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    const nextWorkingDaysError = workingDaysError(workingDays);
    const nextWeeklyOffError = weeklyOffDaysError(weeklyOffDays);
    setWorkingDaysMessage(nextWorkingDaysError ?? "");
    setWeeklyOffMessage(nextWeeklyOffError ?? "");
    setSubmitError("");

    if (nextWorkingDaysError) {
      workingDaysRef.current?.focus();
      return;
    }
    if (nextWeeklyOffError) {
      weeklyOffRef.current?.focus();
      return;
    }

    setSaving(true);
    const submission = ++submissionRef.current;
    const isCurrentSubmission = () =>
      mountedRef.current && submissionRef.current === submission;
    try {
      await updatePayrollSettings({
        dailyRateMethod,
        workingDaysPerMonth: Number(workingDays),
        weeklyOffDays,
      });
      if (!isCurrentSubmission()) return;
      router.push("/app/setup/review");
    } catch (reason) {
      if (isCurrentSubmission()) {
        setSubmitError(reason instanceof Error ? reason.message : "Unable to save payroll settings.");
      }
    } finally {
      if (isCurrentSubmission()) {
        setSaving(false);
      }
    }
  }

  return (
    <SetupWizardShell
      currentStep={2}
      title="Payroll settings"
      description="Choose the defaults used to calculate monthly payroll."
    >
      {loading ? <p className="setup-loading" role="status">Loading payroll settings…</p> : null}
      {loadError ? <p className="setup-alert" role="alert">{loadError}</p> : null}
      {!loading && !loadError ? (
        <form className="setup-form" noValidate onSubmit={submit}>
          <div className="setup-field">
            <span className="setup-field__label">Payroll cycle</span>
            <p className="setup-fixed-value">Monthly</p>
            <p className="setup-field__hint">miniPayroll currently processes one payroll each month.</p>
          </div>

          <div className="setup-field">
            <label htmlFor="working-days">Working days per month</label>
            <input
              ref={workingDaysRef}
              id="working-days"
              type="number"
              min="1"
              max="31"
              step="1"
              inputMode="numeric"
              value={workingDays}
              aria-invalid={Boolean(workingDaysMessage)}
              aria-describedby={workingDaysMessage ? "working-days-error" : "working-days-hint"}
              onChange={(event) => {
                setWorkingDays(event.target.value);
                setWorkingDaysMessage("");
              }}
            />
            <p id="working-days-hint" className="setup-field__hint">Default: 26. Enter 1 to 31.</p>
            {workingDaysMessage ? (
              <p id="working-days-error" className="setup-field__error">{workingDaysMessage}</p>
            ) : null}
          </div>

          <fieldset
            className="setup-fieldset"
            aria-invalid={Boolean(weeklyOffMessage)}
            aria-describedby={weeklyOffMessage ? "weekly-off-error" : "weekly-off-hint"}
          >
            <legend>Weekly off days</legend>
            <p id="weekly-off-hint">Select one or more regular weekly holidays.</p>
            <div className="setup-check-grid">
              {DAYS.map((day, index) => (
                <label key={day} className="setup-check">
                  <input
                    ref={index === 0 ? weeklyOffRef : undefined}
                    type="checkbox"
                    value={day}
                    checked={weeklyOffDays.includes(day)}
                    aria-invalid={Boolean(weeklyOffMessage)}
                    aria-describedby={weeklyOffMessage ? "weekly-off-error" : "weekly-off-hint"}
                    onChange={(event) => {
                      setWeeklyOffDays((current) =>
                        event.target.checked
                          ? DAYS.filter((candidate) => current.includes(candidate) || candidate === day)
                          : current.filter((candidate) => candidate !== day),
                      );
                      setWeeklyOffMessage("");
                    }}
                  />
                  <span>{day}</span>
                </label>
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
              <label className="setup-rate">
                <input
                  type="radio"
                  name="dailyRateMethod"
                  checked={dailyRateMethod === DailyRateMethod.CalendarDays}
                  onChange={() => setDailyRateMethod(DailyRateMethod.CalendarDays)}
                />
                <span>
                  <strong>Calendar days</strong>
                  <small>Daily rate = monthly salary ÷ calendar days in that month.</small>
                </span>
              </label>
              <label className="setup-rate">
                <input
                  type="radio"
                  name="dailyRateMethod"
                  checked={dailyRateMethod === DailyRateMethod.FixedThirty}
                  onChange={() => setDailyRateMethod(DailyRateMethod.FixedThirty)}
                />
                <span>
                  <strong>Fixed 30 days</strong>
                  <small>Daily rate = monthly salary ÷ 30.</small>
                </span>
              </label>
            </div>
          </fieldset>

          {workingDaysMessage || weeklyOffMessage ? (
            <p className="setup-alert" role="alert">Review the highlighted payroll settings.</p>
          ) : null}
          {submitError ? <p className="setup-alert" role="alert">{submitError}</p> : null}
          <div className="setup-actions">
            <button className="setup-button" type="submit" disabled={saving} aria-busy={saving}>
              {saving ? "Saving…" : "Save and continue"}
            </button>
          </div>
        </form>
      ) : null}
    </SetupWizardShell>
  );
}
