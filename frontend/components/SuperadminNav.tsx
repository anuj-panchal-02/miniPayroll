"use client";

import Link from "next/link";

type SuperadminNavProps = {
  pathname: string;
};

const LINKS = [
  { href: "/superadmin", label: "Companies", exact: true },
  { href: "/superadmin/masters/states", label: "States", exact: false },
  { href: "/superadmin/masters/cities", label: "Cities", exact: false },
] as const;

function isCurrent(pathname: string, href: string, exact: boolean) {
  if (exact) {
    return pathname === href || pathname.startsWith("/superadmin/companies");
  }
  return pathname === href || pathname.startsWith(`${href}/`);
}

export function SuperadminNav({ pathname }: SuperadminNavProps) {
  return (
    <nav className="sa-rail" aria-label="Superadmin">
      <Link
        href="/superadmin"
        className="sa-rail__link"
        aria-current={isCurrent(pathname, "/superadmin", true) ? "page" : undefined}
      >
        Companies
      </Link>
      <p className="sa-rail__group">Masters</p>
      {LINKS.filter((link) => !link.exact).map((link) => (
        <Link
          key={link.href}
          href={link.href}
          className="sa-rail__link"
          aria-current={isCurrent(pathname, link.href, false) ? "page" : undefined}
        >
          {link.label}
        </Link>
      ))}
    </nav>
  );
}
