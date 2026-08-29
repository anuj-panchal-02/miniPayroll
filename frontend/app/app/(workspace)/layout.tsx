"use client";

import { CompanyAdminShell } from "@/components/CompanyAdminShell";

export default function CompanyWorkspaceLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return <CompanyAdminShell>{children}</CompanyAdminShell>;
}
