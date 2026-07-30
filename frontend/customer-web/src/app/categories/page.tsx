"use client";

import { useQuery } from "@tanstack/react-query";
import { CategoryTile } from "@/components/CategoryTile";
import { CitySelector } from "@/components/CitySelector";
import { Alert, PageHeading } from "@/components/ui";
import { useSelectedCity } from "@/hooks/useSelectedCity";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import type { CategorySummary } from "@/lib/types";

/** Category listing page (SRS 11.5.1), filtered to the customer's selected city. */
export default function CategoriesPage() {
  const { city } = useSelectedCity();

  return (
    <main className="mx-auto w-full max-w-6xl px-6 py-10">
      <PageHeading title="All categories" subtitle="Browse every service we offer in your city." />

      {city === undefined ? (
        <p className="text-sm text-neutral-500">Loading…</p>
      ) : city === null ? (
        <div className="flex flex-col items-center gap-3 rounded-xl border border-dashed border-black/15 p-10 text-center dark:border-white/20">
          <p className="text-sm text-neutral-600 dark:text-neutral-400">
            Select your city to see the categories available near you.
          </p>
          <CitySelector />
        </div>
      ) : (
        <CategoryGrid cityId={city.id} />
      )}
    </main>
  );
}

function CategoryGrid({ cityId }: { cityId: string }) {
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
    return <p className="text-sm text-neutral-500">No categories are available in your city yet.</p>;
  }

  return (
    <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 md:grid-cols-4">
      {query.data.map((category) => (
        <CategoryTile key={category.id} category={category} />
      ))}
    </div>
  );
}
