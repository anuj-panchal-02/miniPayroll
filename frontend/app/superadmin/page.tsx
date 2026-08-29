"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { CompanyListItem, getToken, listCompanies, setToken } from "@/lib/api";
import { SuperadminShell } from "@/components/SuperadminShell";

export default function SuperadminPage() {
  const router = useRouter();
  const [companies, setCompanies] = useState<CompanyListItem[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    if (!getToken()) {
      router.replace("/login");
      return;
    }

    let cancelled = false;
    async function load() {
      try {
        const items = await listCompanies();
        if (cancelled) {
          return;
        }
        setCompanies(items);
        setError(null);
      } catch (err) {
        if (cancelled) {
          return;
        }
        setError(err instanceof Error ? err.message : "Could not load companies");
        if (String(err).includes("401") || String(err).toLowerCase().includes("unauthorized")) {
          setToken(null);
          router.push("/login");
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void load();
    return () => {
      cancelled = true;
    };
  }, [router]);

  const intro = loading
    ? "Loading companies on this platform."
    : companies.length === 0
      ? "None yet. Create one to start onboarding."
      : `${companies.length} on this platform.`;

  return (
    <SuperadminShell>
      <main className="sa-shell">
        <header className="sa-head sa-head--with-back">
          <h1>Companies</h1>
          <Link href="/superadmin/companies/new" className="sa-compose__submit">
            Create company
          </Link>
          <p>{intro}</p>
        </header>

        <p className="sa-alert" role="alert">
          {error}
        </p>

        {loading ? (
          <ol className="sa-index" aria-busy="true" aria-label="Loading companies">
            <li className="sa-index__row is-skeleton">
              <span className="sa-index__skeleton-bar" />
              <span className="sa-index__skeleton-bar" />
            </li>
            <li className="sa-index__row is-skeleton">
              <span className="sa-index__skeleton-bar" />
            </li>
          </ol>
        ) : companies.length === 0 ? (
          <p className="sa-empty">No companies yet. Create one to start onboarding.</p>
        ) : (
          <ol className="sa-index">
            <li className="sa-index__legend" aria-hidden="true">
              <span>Company</span>
              <span>Contact</span>
              <span>Status</span>
              <span>Limit</span>
            </li>
            {companies.map((company) => (
              <li key={company.id}>
                <Link href={`/superadmin/companies/${company.id}`} className="sa-index__row">
                  <span className="sa-index__name">{company.name}</span>
                  <span className="sa-index__email">{company.contactEmail}</span>
                  <span className="sa-index__meta">
                    <span className="sa-chip" data-status={company.status}>
                      {company.status}
                    </span>
                    <span className="sa-index__limit">{company.employeeLimit}</span>
                  </span>
                </Link>
              </li>
            ))}
          </ol>
        )}
      </main>
    </SuperadminShell>
  );
}
