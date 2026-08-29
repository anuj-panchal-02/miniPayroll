"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { SignOutButton } from "@/components/SignOutButton";
import { BrandLogo } from "@/components/BrandLogo";
import { getMe, getToken, setToken } from "@/lib/api";
import {
  SUPERADMIN_ROLE,
  guardRedirect,
  type RouteGuardOptions,
} from "@/lib/setup";
import "@/app/superadmin/superadmin.css";

type SuperadminShellProps = {
  children: React.ReactNode;
  role?: string;
  homeHref?: string;
  requiredRole?: RouteGuardOptions["requiredRole"];
  allowIncompleteSetup?: boolean;
};

export function SuperadminShell({
  children,
  role = "Superadmin",
  homeHref = "/superadmin",
  requiredRole = SUPERADMIN_ROLE,
  allowIncompleteSetup = false,
}: SuperadminShellProps) {
  const router = useRouter();
  const pathname = usePathname();
  const guardKey = JSON.stringify([
    pathname,
    requiredRole,
    allowIncompleteSetup,
  ]);
  const [validatedGuardKey, setValidatedGuardKey] = useState<string | null>(
    null,
  );

  useEffect(() => {
    if (!getToken()) {
      const redirect = guardRedirect(null, {
        requiredRole,
        allowIncompleteSetup,
        currentPath: pathname,
      });
      if (redirect && redirect !== pathname) {
        router.replace(redirect);
      }
      return;
    }

    let cancelled = false;
    async function check() {
      try {
        const me = await getMe();
        if (cancelled) {
          return;
        }
        const redirect = guardRedirect(me, {
          requiredRole,
          allowIncompleteSetup,
          currentPath: pathname,
        });
        if (redirect) {
          if (redirect !== pathname) {
            router.replace(redirect);
          }
          return;
        }
        setValidatedGuardKey(guardKey);
      } catch {
        if (cancelled) {
          return;
        }
        setToken(null);
        if (pathname !== "/login") {
          router.replace("/login");
        }
      }
    }

    void check();
    return () => {
      cancelled = true;
    };
  }, [
    allowIncompleteSetup,
    guardKey,
    pathname,
    requiredRole,
    router,
  ]);

  if (validatedGuardKey !== guardKey) {
    return (
      <div className="sa">
        <main className="sa-shell">
          <p className="sa-empty" role="status">
            Validating access…
          </p>
        </main>
      </div>
    );
  }

  return (
    <div className="sa">
      <header className="sa-nav">
        <div className="sa-nav__brand">
          <Link href={homeHref} className="sa-nav__logo">
            <BrandLogo size="nav" />
          </Link>
          <p className="sa-nav__role">{role}</p>
        </div>
        <SignOutButton
          onSignOut={() => {
            setToken(null);
            router.push("/login");
          }}
        />
      </header>
      {children}
    </div>
  );
}
