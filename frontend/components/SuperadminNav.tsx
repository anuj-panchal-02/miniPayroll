"use client";

import Link from "next/link";

type SuperadminNavProps = {
  pathname: string;
};

const LINKS = [
  { href: "/superadmin", label: "Companies", exact: true },
  { href: "/superadmin/plan", label: "Plan", exact: true },
  { href: "/superadmin/masters/states", label: "States", exact: false },
  { href: "/superadmin/masters/cities", label: "Cities", exact: false },
] as const;

function isCurrent(pathname: string, href: string, exact: boolean) {
  if (href === "/superadmin") {
    return pathname === href || pathname.startsWith("/superadmin/companies");
  }
  if (exact) {
    return pathname === href || pathname.startsWith(`${href}/`);
  }
  return pathname === href || pathname.startsWith(`${href}/`);
}

export function SuperadminNav({ pathname }: SuperadminNavProps) {
  return (
    <nav className="sa-rail" aria-label="Superadmin">
      {LINKS.filter((link) => link.exact).map((link) => (
        <Link
          key={link.href}
          href={link.href}
          className="sa-rail__link"
          aria-current={isCurrent(pathname, link.href, true) ? "page" : undefined}
        >
          {link.label}
        </Link>
      ))}
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
