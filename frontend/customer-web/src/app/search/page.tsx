"use client";

import { useQuery } from "@tanstack/react-query";
import { motion } from "motion/react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { Suspense } from "react";
import { CategoryGridSkeleton } from "@/components/CategoryGridSkeleton";
import { CategoryTile } from "@/components/CategoryTile";
import { Reveal, revealItem } from "@/components/motion";
import { SearchBar } from "@/components/SearchBar";
import { ServiceCard } from "@/components/ServiceCard";
import { Alert, Button, EmptyState, PageHeading, Skeleton } from "@/components/ui";
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
    <Suspense fallback={<SearchFallback />}>
      <SearchResults />
    </Suspense>
  );
}

/**
 * Matches the real page's frame rather than rendering an empty box, so the
 * heading and search field don't pop in after the boundary resolves.
 */
function SearchFallback() {
  return (
    <main className="mx-auto w-full max-w-7xl px-4 py-8 sm:px-6 sm:py-12">
      <Skeleton className="h-9 w-64" />
      <Skeleton className="mt-6 h-[3.25rem] w-full max-w-xl rounded-xl" />
      <div className="mt-10">
        <CategoryGridSkeleton count={4} />
      </div>
    </main>
  );
}

function SearchResults() {
  const params = useSearchParams();
  const q = params.get("q") ?? "";
  const trimmed = q.trim();
  const isSearchable = trimmed.length >= 2;

  const query = useQuery({
    queryKey: ["catalog-search", q],
    queryFn: () =>
      apiFetch<CatalogSearchResult>(`${API_V1}/catalog/search?q=${encodeURIComponent(q)}`),
    enabled: isSearchable,
  });

  const resultCount = query.data
    ? query.data.categories.length + query.data.services.length
    : 0;

  return (
    <main className="mx-auto w-full max-w-7xl px-4 py-8 sm:px-6 sm:py-12">
      <PageHeading
        title={trimmed ? `Results for “${trimmed}”` : "Search"}
        subtitle={
          isSearchable && query.isSuccess
            ? `${resultCount} ${resultCount === 1 ? "match" : "matches"} found`
            : "Find a service or browse by category."
        }
      />

      <div className="mb-10 max-w-xl">
        <SearchBar defaultValue={q} autoFocus={!trimmed} />
      </div>

      {!isSearchable ? (
        <EmptyState
          title="Type at least 2 characters"
          description="Search for a service by name — for example “AC repair” or “deep cleaning”."
        />
      ) : query.isPending ? (
        <SearchResultsSkeleton />
      ) : query.isError ? (
        <Alert
          tone="error"
          title="Search failed"
          action={
            <Button size="sm" variant="secondary" onClick={() => query.refetch()}>
              Retry
            </Button>
          }
        >
          {describeError(query.error)}
        </Alert>
      ) : resultCount === 0 ? (
        <EmptyState
          title="No matches"
          description={`Nothing matched “${trimmed}”. Try a different word, or browse all categories.`}
          // A Link, not a Button with a router push: this is a plain
          // navigation and must stay middle-clickable.
          action={
            <Link
              href="/categories"
              className="inline-flex h-10 items-center rounded-lg border border-line bg-surface px-4 text-sm font-medium text-fg shadow-xs transition duration-fast ease-out hover:border-line-strong hover:bg-surface-2"
            >
              Browse categories
            </Link>
          }
        />
      ) : (
        <div className="flex flex-col gap-12">
          {query.data.categories.length > 0 ? (
            <section aria-labelledby="search-categories-heading">
              <h2
                id="search-categories-heading"
                className="mb-4 text-lg font-semibold tracking-tight text-fg"
              >
                Categories
                <span className="ml-2 text-sm font-normal text-fg-subtle">
                  {query.data.categories.length}
                </span>
              </h2>
              <Reveal className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
                {query.data.categories.map((category) => (
                  <motion.div key={category.id} variants={revealItem}>
                    <CategoryTile category={category} />
                  </motion.div>
                ))}
              </Reveal>
            </section>
          ) : null}

          {query.data.services.length > 0 ? (
            <section aria-labelledby="search-services-heading">
              <h2
                id="search-services-heading"
                className="mb-4 text-lg font-semibold tracking-tight text-fg"
              >
                Services
                <span className="ml-2 text-sm font-normal text-fg-subtle">
                  {query.data.services.length}
                </span>
              </h2>
              <Reveal className="grid grid-cols-1 gap-4 md:grid-cols-2">
                {query.data.services.map((service) => (
                  <motion.div key={service.id} variants={revealItem}>
                    <ServiceCard
                      slug={service.slug}
                      name={service.name}
                      description={service.description}
                      price={service.price}
                      durationMinutes={service.durationMinutes}
                      coverImageUrl={service.coverImageUrl}
                    />
                  </motion.div>
                ))}
              </Reveal>
            </section>
          ) : null}
        </div>
      )}
    </main>
  );
}

function SearchResultsSkeleton() {
  return (
    <div className="flex flex-col gap-12">
      <div>
        <Skeleton className="mb-4 h-6 w-32" />
        <CategoryGridSkeleton count={4} />
      </div>
      <div>
        <Skeleton className="mb-4 h-6 w-28" />
        <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
          {Array.from({ length: 4 }, (_, index) => (
            <Skeleton key={index} className="h-72 rounded-2xl" />
          ))}
        </div>
      </div>
    </div>
  );
}
