"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { Suspense } from "react";
import { CategoryTile } from "@/components/CategoryTile";
import { Alert, PageHeading } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import type { CatalogSearchResult } from "@/lib/types";

/**
 * Global search results (SRS 11.1.3, 11.5-11.6, 24.3).
 *
 * Wrapped in Suspense: useSearchParams opts the tree below it out of static
 * rendering, and Next's App Router requires a Suspense boundary around that
 * or the production build fails.
 */
export default function SearchPage() {
  return (
    <Suspense fallback={<main className="mx-auto w-full max-w-6xl px-6 py-10" />}>
      <SearchResults />
    </Suspense>
  );
}

function SearchResults() {
  const params = useSearchParams();
  const q = params.get("q") ?? "";

  const query = useQuery({
    queryKey: ["catalog-search", q],
    queryFn: () => apiFetch<CatalogSearchResult>(`${API_V1}/catalog/search?q=${encodeURIComponent(q)}`),
    enabled: q.trim().length >= 2,
  });

  return (
    <main className="mx-auto w-full max-w-6xl px-6 py-10">
      <PageHeading title={`Search results for "${q}"`} />

      {q.trim().length < 2 ? (
        <p className="text-sm text-neutral-500">Type at least 2 characters to search.</p>
      ) : query.isPending ? (
        <p className="text-sm text-neutral-500">Searching…</p>
      ) : query.isError ? (
        <Alert>{describeError(query.error)}</Alert>
      ) : query.data.categories.length === 0 && query.data.services.length === 0 ? (
        <p className="text-sm text-neutral-500">No categories or services matched your search.</p>
      ) : (
        <div className="flex flex-col gap-10">
          {query.data.categories.length > 0 ? (
            <section aria-labelledby="search-categories-heading">
              <h2 id="search-categories-heading" className="mb-4 text-lg font-semibold">
                Categories
              </h2>
              <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 md:grid-cols-4">
                {query.data.categories.map((category) => (
                  <CategoryTile key={category.id} category={category} />
                ))}
              </div>
            </section>
          ) : null}

          {query.data.services.length > 0 ? (
            <section aria-labelledby="search-services-heading">
              <h2 id="search-services-heading" className="mb-4 text-lg font-semibold">
                Services
              </h2>
              <ul className="flex flex-col gap-3">
                {query.data.services.map((service) => (
                  <li key={service.id}>
                    <Link
                      href={`/services/${service.slug}`}
                      className="flex items-center justify-between rounded-xl border border-black/10 bg-white p-4 hover:border-black/25 dark:border-white/15 dark:bg-neutral-900 dark:hover:border-white/30"
                    >
                      <div>
                        <p className="text-sm font-medium">{service.name}</p>
                        <p className="mt-0.5 text-sm text-neutral-600 dark:text-neutral-400">
                          {service.description}
                        </p>
                      </div>
                      <p className="shrink-0 pl-4 text-sm font-semibold">₹{service.price}</p>
                    </Link>
                  </li>
                ))}
              </ul>
            </section>
          ) : null}
        </div>
      )}
    </main>
  );
}
