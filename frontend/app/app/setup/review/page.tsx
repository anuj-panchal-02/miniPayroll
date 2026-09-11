"use client";

import { useEffect, useRef, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { SetupWizardShell } from "@/components/SetupWizardShell";
import { useToast } from "@/components/Toast";
import { Button } from "@/components/ui/Button";
import { Skeleton } from "@/components/ui/Skeleton";
import {
  completeCompanySetup,
  getCompanySetup,
  type CompanySetup,
} from "@/lib/api";

export default function ReviewSetupPage() {
  const router = useRouter();
  const [setup, setSetup] = useState<CompanySetup | null>(null);
  const [loadError, setLoadError] = useState("");
  const toast = useToast();
  const [completing, setCompleting] = useState(false);
  const mountedRef = useRef(false);
  const submissionRef = useRef(0);

  useEffect(() => {
    mountedRef.current = true;
    let cancelled = false;
    getCompanySetup()
      .then((savedSetup) => {
        if (!cancelled) {
          setSetup(savedSetup);
        }
      })
      .catch((reason) => {
        if (!cancelled) {
          setLoadError(
            reason instanceof Error
              ? reason.message
              : "Unable to load the setup summary.",
          );
        }
      });
    return () => {
      cancelled = true;
      mountedRef.current = false;
      submissionRef.current += 1;
    };
  }, []);

  async function completeSetup() {
    toast.dismiss();
    setCompleting(true);
    const submission = ++submissionRef.current;
    const isCurrentSubmission = () =>
      mountedRef.current && submissionRef.current === submission;
    try {
      await completeCompanySetup();
      if (!isCurrentSubmission()) return;
      router.replace("/app");
    } catch (reason) {
      if (isCurrentSubmission()) {
        toast.showError(
          reason instanceof Error
            ? reason.message
            : "Unable to complete company setup.",
        );
      }
    } finally {
      if (isCurrentSubmission()) {
        setCompleting(false);
      }
    }
  }

  return (
    <SetupWizardShell
      currentStep={3}
      title="Review and complete"
      description="Confirm your company and payroll settings before opening the workspace."
    >
      {!setup && !loadError ? (
        <div className="space-y-3" role="status">
          <Skeleton className="h-24 w-full" />
          <Skeleton className="h-24 w-full" />
        </div>
      ) : null}
      {loadError ? (
        <p className="setup-alert" role="alert">
          {loadError}
        </p>
      ) : null}
      {setup ? (
        <div className="setup-review">
          <section className="setup-summary" aria-labelledby="company-summary-title">
            <div className="setup-summary__heading">
              <h2 id="company-summary-title">Company details</h2>
              <Link href="/app/setup/company">Edit company details</Link>
            </div>
            <div className="setup-summary__company">
              {setup.logoUrl ? (
                // eslint-disable-next-line @next/next/no-img-element
                <img
                  className="setup-summary__logo"
                  src={setup.logoUrl}
                  alt={`${setup.name} logo`}
                />
              ) : (
                <div className="setup-logo__placeholder" aria-label="No company logo">
                  No logo
                </div>
              )}
              <dl className="setup-summary__facts">
                <div>
                  <dt>Company name</dt>
                  <dd>{setup.name}</dd>
                </div>
                <div>
                  <dt>Contact</dt>
                  <dd>{setup.contactEmail}</dd>
                  <dd>{setup.contactPhone || "Not provided"}</dd>
                </div>
                <div>
                  <dt>Address</dt>
                  <dd>
                    {[
                      setup.addressLine1,
                      setup.addressLine2,
                      setup.city,
                      setup.state,
                      setup.postalCode,
                    ]
                      .filter(Boolean)
                      .join(", ")}
                  </dd>
                </div>
              </dl>
            </div>
          </section>

          <section className="setup-summary" aria-labelledby="payroll-summary-title">
            <div className="setup-summary__heading">
              <h2 id="payroll-summary-title">Payroll settings</h2>
              <Link href="/app/setup/payroll">Edit payroll settings</Link>
            </div>
            <dl className="setup-summary__facts">
              <div>
                <dt>Payroll cycle</dt>
                <dd>{setup.payrollCycle}</dd>
              </div>
              <div>
                <dt>Working days per month</dt>
                <dd>{setup.workingDaysPerMonth}</dd>
              </div>
              <div>
                <dt>Weekly off</dt>
                <dd>{setup.weeklyOffDays.join(", ")}</dd>
              </div>
              <div>
                <dt>Daily rate method</dt>
                <dd>
                  {setup.dailyRateMethod === "FixedThirty"
                    ? "Fixed 30 days"
                    : "Calendar days"}
                </dd>
              </div>
              <div>
                <dt>Provident Fund</dt>
                <dd>
                  {setup.pfApplicable
                    ? setup.pfUseWageCeiling
                      ? "On, ₹15,000 wage ceiling"
                      : "On, full Basic + DA"
                    : "Off"}
                </dd>
              </div>
              <div>
                <dt>ESI</dt>
                <dd>{setup.esiApplicable ? "On" : "Off"}</dd>
              </div>
              <div>
                <dt>Professional tax / LWF</dt>
                <dd>{setup.state ? `${setup.state} rules` : "From company state"}</dd>
              </div>
            </dl>
          </section>

          <div className="setup-actions">
            <Button
              type="button"
              loading={completing}
              loadingLabel="Completing…"
              onClick={() => void completeSetup()}
            >
              Complete setup
            </Button>
          </div>
        </div>
      ) : null}
    </SetupWizardShell>
  );
}
