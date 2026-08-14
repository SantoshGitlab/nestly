"use client";

import { useQuery } from "@tanstack/react-query";
import { motion } from "motion/react";
import { CategoryGridSkeleton } from "@/components/CategoryGridSkeleton";
import { CategoryTile } from "@/components/CategoryTile";
import { CitySelector } from "@/components/CitySelector";
import { PageBanner } from "@/components/PageBanner";
import { Reveal, revealItem } from "@/components/motion";
import { Alert, Button, EmptyState } from "@/components/ui";
import { useSelectedCity } from "@/hooks/useSelectedCity";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import type { CategorySummary } from "@/lib/types";

/**
 * Category listing page (SRS 11.5.1), filtered to the customer's selected
 * city. `PageBanner` here has no single category to key off — it uses the
 * first serviceable category with a `pageBannerUrl` set as a representative
 * image (dynamic, not hardcoded to one category's slug, so it keeps working
 * as the catalog changes) rather than the category-specific banner the
 * detail and checkout pages show.
 */
export default function CategoriesPage() {
  const { city } = useSelectedCity();

  return (
    <main className="flex w-full flex-col">
      {/* `undefined` is "still reading the persisted city", `null` is "read,
          none chosen" — collapsing them would flash the picker at customers
          who already have a city saved. */}
      {city === undefined ? (
        <>
          <PageBanner title="All categories" description="Browse every service we offer in your city." />
          <div className="mx-auto w-full max-w-7xl px-4 py-8 sm:px-6 sm:py-12">
            <CategoryGridSkeleton />
          </div>
        </>
      ) : city === null ? (
        <>
          <PageBanner title="All categories" description="Browse every service we offer in your city." />
          <div className="mx-auto w-full max-w-7xl px-4 py-8 sm:px-6 sm:py-12">
            <EmptyState
              title="Choose your city"
              description="Services and pricing vary by city — tell us where you are and we'll show what's available near you."
              action={<CitySelector />}
            />
          </div>
        </>
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

  const bannerImageUrl = query.data?.find((category) => category.pageBannerUrl)?.pageBannerUrl;

  return (
    <>
      <PageBanner
        title="All categories"
        description="Browse every service we offer in your city."
        imageUrl={bannerImageUrl}
        badge={<CitySelector />}
      />

      <div className="mx-auto w-full max-w-7xl px-4 py-8 sm:px-6 sm:py-12">
        {query.isPending ? (
          <CategoryGridSkeleton />
        ) : query.isError ? (
          <Alert
            tone="error"
            title="Couldn't load categories"
            action={
              <Button size="sm" variant="secondary" onClick={() => query.refetch()}>
                Retry
              </Button>
            }
          >
            {describeError(query.error)}
          </Alert>
        ) : query.data.length === 0 ? (
          <EmptyState
            title="No services here yet"
            description="We're not live in your city yet — try another city, or check back soon."
            action={<CitySelector />}
          />
        ) : (
          <Reveal className="grid grid-cols-1 gap-5 sm:grid-cols-2 lg:grid-cols-3">
            {query.data.map((category) => (
              <motion.div key={category.id} variants={revealItem}>
                <CategoryTile category={category} />
              </motion.div>
            ))}
          </Reveal>
        )}
      </div>
    </>
  );
}
