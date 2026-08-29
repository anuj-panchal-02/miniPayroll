"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { SetupWizardShell } from "@/components/SetupWizardShell";
import { getCompanySetup } from "@/lib/api";
import { setupPath } from "@/lib/setup";

export default function SetupPage() {
  const router = useRouter();
  const [error, setError] = useState("");

  useEffect(() => {
    let cancelled = false;

    async function load() {
      try {
        const setup = await getCompanySetup();
        if (!cancelled) {
          router.replace(setup.isSetupComplete ? "/app" : setupPath(setup.setupStep));
        }
      } catch (reason) {
        if (!cancelled) {
          setError(reason instanceof Error ? reason.message : "Unable to load company setup.");
        }
      }
    }

    void load();
    return () => {
      cancelled = true;
    };
  }, [router]);

  return (
    <SetupWizardShell
      currentStep={1}
      title="Loading company setup"
      description="We are returning you to your saved setup step."
    >
      <p className={error ? "setup-alert" : "setup-loading"} role={error ? "alert" : "status"}>
        {error || "Loading…"}
      </p>
    </SetupWizardShell>
  );
}
