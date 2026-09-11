"use client";

import { SuperadminShell } from "@/components/SuperadminShell";

export default function SetupLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return (
    <SuperadminShell
      role="Company Admin"
      homeHref="/app"
      requiredRole="CompanyAdmin"
      allowIncompleteSetup
    >
      {children}
    </SuperadminShell>
  );
}
