"use client";

import { SuperadminShell } from "@/components/SuperadminShell";

export default function SuperadminLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  return <SuperadminShell>{children}</SuperadminShell>;
}
