"use client";

import { useQuery } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { Alert, Button, Field, Skeleton, cx } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import { setSelectedLocality } from "@/lib/location";
import type { LocalitySearchResult } from "@/lib/types";

/**
 * Locality picker (SRS 11.4.1 - pincode selection), scoped to the customer's
 * chosen city. Resolves a localityId by name/pincode search rather than
 * asking the customer to know it directly - the slot and serviceability
 * APIs key off it (a locality's serviceability follows its parent pincode).
 */
export function LocalitySelector({ cityId }: { cityId: string }) {
  const [search, setSearch] = useState("");
  const [debouncedSearch, setDebouncedSearch] = useState("");
  const [selectedId, setSelectedId] = useState<string | null>(null);

  // The search term is part of the query key, so without this every keystroke
  // fired a fresh request at the geography API and raced the previous one.
  useEffect(() => {
    const timer = window.setTimeout(() => setDebouncedSearch(search), 250);
    return () => window.clearTimeout(timer);
  }, [search]);

  const trimmed = debouncedSearch.trim();
  const query = useQuery({
    queryKey: ["geography", "localities", cityId, trimmed],
    queryFn: () =>
      apiFetch<LocalitySearchResult[]>(
        `${API_V1}/geography/cities/${cityId}/localities${trimmed ? `?search=${encodeURIComponent(trimmed)}` : ""}`,
      ),
    // Keeps the previous list on screen while a new term loads, instead of
    // collapsing to a skeleton on every refinement.
    placeholderData: (previous) => previous,
  });

  return (
    <div className="flex flex-col gap-3">
      <Field
        label="Find your locality"
        name="locality-search"
        type="text"
        value={search}
        onChange={(event) => setSearch(event.target.value)}
        placeholder="Locality name or pincode"
        hint="We use this to check availability and show accurate slots."
        leading={
          <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            className="h-4 w-4"
            aria-hidden
          >
            <circle cx="11" cy="11" r="7" />
            <path d="m20 20-3.2-3.2" />
          </svg>
        }
      />

      {query.isPending ? (
        <div className="flex flex-col gap-1.5">
          {Array.from({ length: 4 }, (_, index) => (
            <Skeleton key={index} className="h-10 w-full" />
          ))}
        </div>
      ) : query.isError ? (
        <Alert
          tone="error"
          action={
            <Button size="sm" variant="secondary" onClick={() => query.refetch()}>
              Retry
            </Button>
          }
        >
          {describeError(query.error)}
        </Alert>
      ) : query.data.length === 0 ? (
        <p className="rounded-lg bg-surface-2 px-3 py-2.5 text-sm text-fg-muted">
          No localities matched. Try a different name or pincode.
        </p>
      ) : (
        <ul
          className={cx(
            "flex max-h-56 flex-col gap-1 overflow-y-auto transition-opacity duration-fast",
            // Dim rather than blank while a refined term is in flight.
            query.isPlaceholderData && "opacity-60",
          )}
        >
          {query.data.map((locality) => {
            const isSelected = locality.id === selectedId;
            return (
              <li key={locality.id}>
                <button
                  type="button"
                  onClick={() => {
                    setSelectedLocality({
                      id: locality.id,
                      name: locality.name,
                      pincodeId: locality.pincodeId,
                    });
                    setSelectedId(locality.id);
                  }}
                  aria-current={isSelected ? "true" : undefined}
                  className={cx(
                    "flex w-full items-center justify-between gap-3 rounded-lg px-3 py-2.5 text-left text-sm transition-colors duration-fast ease-out",
                    isSelected
                      ? "bg-brand-50 font-medium text-brand-700 dark:bg-brand-500/15 dark:text-brand-300"
                      : "text-fg hover:bg-surface-2",
                  )}
                >
                  <span className="min-w-0">
                    <span className="block truncate">{locality.name}</span>
                    <span className="block truncate text-xs text-fg-subtle">
                      {locality.zoneName} · {locality.pincodeCode}
                    </span>
                  </span>
                  {isSelected ? (
                    <svg
                      viewBox="0 0 24 24"
                      fill="none"
                      stroke="currentColor"
                      strokeWidth="2.25"
                      strokeLinecap="round"
                      strokeLinejoin="round"
                      className="h-4 w-4 shrink-0"
                      aria-hidden
                    >
                      <path d="m5 13 4 4L19 7" />
                    </svg>
                  ) : null}
                </button>
              </li>
            );
          })}
        </ul>
      )}
    </div>
  );
}
