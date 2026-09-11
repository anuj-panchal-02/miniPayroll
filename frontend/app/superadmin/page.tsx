"use client";

import { useEffect, useMemo, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { CompanyListItem, listCompanies, setToken } from "@/lib/api";
import { Alert } from "@/components/ui/Alert";
import { ListPager, usePager } from "@/components/ui/ListPager";
import { ListToolbar } from "@/components/ui/ListToolbar";
import { pageSlice } from "@/lib/paging";

export default function SuperadminPage() {
  const router = useRouter();
  const [companies, setCompanies] = useState<CompanyListItem[]>([]);
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const [query, setQuery] = useState("");
  const [status, setStatus] = useState("all");

  useEffect(() => {
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

  const filtered = useMemo(() => {
    const needle = query.trim().toLowerCase();
    return companies.filter((company) => {
      const matchesStatus = status === "all" || company.status === status;
      const matchesQuery =
        !needle ||
        company.name.toLowerCase().includes(needle) ||
        company.contactEmail.toLowerCase().includes(needle);
      return matchesStatus && matchesQuery;
    });
  }, [companies, query, status]);

  const pager = usePager(filtered.length, `${query}|${status}`);
  const visible = pageSlice(filtered, pager.page, pager.pageSize);

  const intro = loading
    ? "Loading companies on this platform."
    : companies.length === 0
      ? "None yet. Create one to start onboarding."
      : `${companies.length} on this platform.`;
  const filtering = Boolean(query.trim()) || status !== "all";
  const resultText = loading
    ? "Loading companies."
    : filtered.length === 1
      ? "1 company"
      : `${filtered.length} companies`;

  return (
    <main className="sa-shell">
        <header className="sa-head sa-head--with-back">
          <h1>Companies</h1>
          <Link href="/superadmin/companies/new" className="sa-compose__submit">
            Create company
          </Link>
          <p>{intro}</p>
        </header>
        <Alert>{error}</Alert>

        {!loading && companies.length > 0 ? (
          <ListToolbar
            searchId="company-search"
            searchLabel="Search companies"
            searchValue={query}
            searchPlaceholder="Name or contact email"
            onSearchChange={setQuery}
            filterId="company-status"
            filterLabel="Status"
            filterValue={status}
            filterOptions={[
              { value: "all", label: "All statuses" },
              { value: "Pending", label: "Pending" },
              { value: "Active", label: "Active" },
            ]}
            onFilterChange={setStatus}
            resultText={resultText}
            showReset={filtering}
            onReset={() => {
              setQuery("");
              setStatus("all");
            }}
          />
        ) : null}

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
        ) : filtered.length === 0 ? (
          <p className="sa-empty">No companies match this search.</p>
        ) : (
          <>
          <ol className="sa-index">
            <li className="sa-index__legend" aria-hidden="true">
              <span>Company</span>
              <span>Contact</span>
              <span>Status</span>
              <span>Limit</span>
            </li>
            {visible.map((company) => (
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
          <ListPager
            id="companies"
            page={pager.page}
            pageSize={pager.pageSize}
            total={filtered.length}
            onPageChange={pager.setPage}
            onPageSizeChange={pager.setPageSize}
          />
          </>
        )}
    </main>
  );
}
