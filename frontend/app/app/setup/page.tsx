"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { SetupWizardShell } from "@/components/SetupWizardShell";
import { Skeleton } from "@/components/ui/Skeleton";
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
      <div className={error ? "setup-alert" : undefined} role={error ? "alert" : "status"}>
        {error ? (
          error
        ) : (
          <div className="space-y-3">
            <Skeleton className="h-11 w-full" />
            <Skeleton className="h-11 w-2/3" />
          </div>
        )}
      </div>
    </SetupWizardShell>
  );
}
