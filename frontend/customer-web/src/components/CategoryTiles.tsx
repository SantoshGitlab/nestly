"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { CategoryTile } from "@/components/CategoryTile";
import { CitySelector } from "@/components/CitySelector";
import { Alert } from "@/components/ui";
import { useSelectedCity } from "@/hooks/useSelectedCity";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import type { CategorySummary } from "@/lib/types";

/**
 * Category tile grid, filtered to the customer's selected city (SRS 11.1.3 -
 * "homepage shall display service categories filtered by selected
 * city/serviceability where applicable").
 */
export function CategoryTiles() {
  const { city } = useSelectedCity();

  if (city === undefined) {
    return <p className="text-sm text-neutral-500">Loading…</p>;
  }

  if (city === null) {
    return <NoCitySelectedPrompt />;
  }

  return <CityCategoryGrid cityId={city.id} />;
}

function NoCitySelectedPrompt() {
  return (
    <div className="flex flex-col items-center gap-3 rounded-xl border border-dashed border-black/15 p-10 text-center dark:border-white/20">
      <p className="text-sm text-neutral-600 dark:text-neutral-400">
        Select your city to see the services available near you.
      </p>
      <CitySelector />
    </div>
  );
}

function CityCategoryGrid({ cityId }: { cityId: string }) {
  const [showAll, setShowAll] = useState(false);
  const query = useQuery({
    queryKey: ["categories", cityId],
    queryFn: () => apiFetch<CategorySummary[]>(`${API_V1}/categories?cityId=${cityId}`),
  });

  if (query.isPending) {
    return <p className="text-sm text-neutral-500">Loading categories…</p>;
  }

  if (query.isError) {
    return <Alert>{describeError(query.error)}</Alert>;
  }

  if (query.data.length === 0) {
    return (
      <p className="text-sm text-neutral-500">
        No services are available in your city yet - check back soon.
      </p>
    );
  }

  const visible = showAll ? query.data : query.data.slice(0, 8);

  return (
    <div className="flex flex-col gap-4">
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 md:grid-cols-4">
        {visible.map((category) => (
          <CategoryTile key={category.id} category={category} />
        ))}
      </div>

      {!showAll && query.data.length > visible.length ? (
        <button
          type="button"
          onClick={() => setShowAll(true)}
          className="mx-auto text-sm font-medium hover:underline"
        >
          Show all {query.data.length} categories
        </button>
      ) : null}
    </div>
  );
}
