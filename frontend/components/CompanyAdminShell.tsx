"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { SuperadminShell } from "@/components/SuperadminShell";

const LINKS = [
  { href: "/app", label: "Dashboard" },
  { href: "/app/employees", label: "Employees" },
] as const;

const COMING_SOON = ["Payroll", "Settings"] as const;

type CompanyAdminShellProps = {
  children: React.ReactNode;
};

export function CompanyAdminShell({ children }: CompanyAdminShellProps) {
  const pathname = usePathname();

  return (
    <SuperadminShell
      role="Company Admin"
      homeHref="/app"
      requiredRole="CompanyAdmin"
    >
      <div className="sa-workspace">
        <nav className="sa-sidebar" aria-label="Company">
          {LINKS.map((link) => {
            const current =
              link.href === "/app"
                ? pathname === "/app"
                : pathname === link.href || pathname.startsWith(`${link.href}/`);
            return (
              <Link
                key={link.href}
                href={link.href}
                aria-current={current ? "page" : undefined}
              >
                {link.label}
              </Link>
            );
          })}
          {COMING_SOON.map((label) => (
            <span key={label} className="sa-sidebar__soon">
              {label} · Coming soon
            </span>
          ))}
        </nav>
        <div className="sa-workspace__main">{children}</div>
      </div>
    </SuperadminShell>
  );
}
