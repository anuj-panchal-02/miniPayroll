"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { SignOutButton } from "@/components/SignOutButton";
import { BrandLogo } from "@/components/BrandLogo";
import { SuperadminNav } from "@/components/SuperadminNav";
import { getToken, setToken, type MeResponse } from "@/lib/api";
import { clearSession, loadSession, readSession } from "@/lib/session";
import {
  LOGIN_PATH,
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
  const [session, setSession] = useState<MeResponse | null>(() => readSession());

  useEffect(() => {
    if (!getToken()) {
      clearSession();
      setSession(null);
      if (pathname !== LOGIN_PATH) {
        router.replace(LOGIN_PATH);
      }
      return;
    }

    const cached = readSession();
    if (cached) {
      setSession(cached);
      return;
    }

    let cancelled = false;
    loadSession()
      .then((me) => {
        if (!cancelled) {
          setSession(me);
        }
      })
      .catch(() => {
        if (cancelled) {
          return;
        }
        setToken(null);
        setSession(null);
        if (pathname !== LOGIN_PATH) {
          router.replace(LOGIN_PATH);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [allowIncompleteSetup, pathname, requiredRole, router]);

  const redirect = session
    ? guardRedirect(session, {
        requiredRole,
        allowIncompleteSetup,
        currentPath: pathname,
      })
    : null;

  useEffect(() => {
    if (redirect && redirect !== pathname) {
      router.replace(redirect);
    }
  }, [pathname, redirect, router]);

  const showPlatformNav = requiredRole === SUPERADMIN_ROLE;
  const showChildren = Boolean(session) && !redirect;
  const main = showChildren ? (
    children
  ) : (
    <main className="sa-shell">
      <p className="sa-empty" role="status">
        Validating access…
      </p>
    </main>
  );

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
      {showPlatformNav ? (
        <div className="sa-layout">
          <SuperadminNav pathname={pathname} />
          {main}
        </div>
      ) : (
        main
      )}
    </div>
  );
}
