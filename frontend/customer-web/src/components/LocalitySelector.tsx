"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
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

  const query = useQuery({
    queryKey: ["geography", "localities", cityId, search],
    queryFn: () =>
      apiFetch<LocalitySearchResult[]>(
        `${API_V1}/geography/cities/${cityId}/localities${search.trim() ? `?search=${encodeURIComponent(search.trim())}` : ""}`,
      ),
  });

  return (
    <div className="flex flex-col gap-2">
      <label htmlFor="locality-search" className="text-sm font-medium">
        Find your locality
      </label>
      <input
        id="locality-search"
        type="text"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        placeholder="Locality name or pincode"
        className="rounded-lg border border-black/15 bg-transparent px-3 py-2 text-sm outline-none focus:border-black focus:ring-1 focus:ring-black dark:border-white/20 dark:focus:border-white dark:focus:ring-white"
      />

      {query.isPending ? (
        <p className="text-xs text-neutral-500">Loading localities…</p>
      ) : query.isError ? (
        <p className="text-xs text-red-600 dark:text-red-400">{describeError(query.error)}</p>
      ) : query.data.length === 0 ? (
        <p className="text-xs text-neutral-500">No localities matched. Try a different name or pincode.</p>
      ) : (
        <ul className="flex max-h-48 flex-col gap-1 overflow-y-auto">
          {query.data.map((locality) => (
            <li key={locality.id}>
              <button
                type="button"
                onClick={() =>
                  setSelectedLocality({
                    id: locality.id,
                    name: locality.name,
                    pincodeId: locality.pincodeId,
                  })
                }
                className="w-full rounded-lg px-3 py-2 text-left text-sm hover:bg-black/5 dark:hover:bg-white/10"
              >
                {locality.name}
                <span className="ml-1 text-neutral-500">
                  · {locality.zoneName} · {locality.pincodeCode}
                </span>
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
