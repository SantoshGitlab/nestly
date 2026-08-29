"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { motion } from "motion/react";
import { useState } from "react";
import { Reveal, revealItem } from "@/components/motion";
import { useSelectedCity } from "@/hooks/useSelectedCity";
import { API_V1, apiFetch } from "@/lib/api";
import type { CategorySummary } from "@/lib/types";

/**
 * "Home Services, Right at Your Doorstep": every registered category
 * serviceable in the customer's selected city/locality, as a small animated
 * icon row overlaid on the hero. Same data source as the categories listing
 * (`GET /categories?cityId=`), just rendered as compact icons instead of full
 * cards - this is a quick-jump rail, not a second category grid.
 *
 * Silent on every "nothing to show" case (no city yet, no categories, load
 * error): it sits on top of the hero banner, so a skeleton/empty-state block
 * here would compete with the hero's own content rather than complementing
 * it. The full grid with proper empty/error states still lives in the
 * "New & Trending" section and the /categories page.
 */
export function CategoryQuickPicks() {
  const { city, locality } = useSelectedCity();

  if (!city) {
    return null;
  }

  return <QuickPicksRow cityId={city.id} pincodeId={locality?.pincodeId} />;
}

function QuickPicksRow({ cityId, pincodeId }: { cityId: string; pincodeId?: string }) {
  const query = useQuery({
    queryKey: ["categories", cityId, pincodeId],
    queryFn: () =>
      apiFetch<CategorySummary[]>(
        `${API_V1}/categories?cityId=${cityId}${pincodeId ? `&pincodeId=${pincodeId}` : ""}`,
      ),
    // Home page (CategoryTiles) may already have this cached under the same
    // key - a shared cache entry means this rail never double-fetches.
    staleTime: 60 * 1000,
  });

  if (!query.data || query.data.length === 0) {
    return null;
  }

  return (
    <div className="flex flex-col gap-2 text-left">
      <p className="text-xs font-semibold uppercase tracking-wide text-white/80 drop-shadow-sm">
        Home Services, Right at Your Doorstep
      </p>
      <Reveal className="flex max-w-full flex-wrap gap-3">
        {query.data.slice(0, 10).map((category) => (
          <motion.div key={category.id} variants={revealItem}>
            <QuickPickIcon category={category} />
          </motion.div>
        ))}
      </Reveal>
    </div>
  );
}

function QuickPickIcon({ category }: { category: CategorySummary }) {
  const [imageFailed, setImageFailed] = useState(false);
  const showImage = !!category.iconUrl && !imageFailed;

  return (
    <Link
      href={`/categories/${category.slug}`}
      title={category.name}
      className="group flex flex-col items-center gap-1"
    >
      <motion.span
        whileHover={{ scale: 1.08, y: -2 }}
        whileTap={{ scale: 0.95 }}
        className="flex h-[50px] w-[50px] shrink-0 items-center justify-center overflow-hidden rounded-xl bg-white/95 shadow-md ring-1 ring-black/5 transition-shadow duration-fast ease-out group-hover:shadow-lg dark:bg-surface/95"
      >
        {showImage ? (
          // eslint-disable-next-line @next/next/no-img-element -- admin-supplied external URL, unsuited to static optimization.
          <img
            src={category.iconUrl!}
            alt=""
            loading="lazy"
            decoding="async"
            onError={() => setImageFailed(true)}
            className="h-full w-full object-cover"
          />
        ) : (
          <span aria-hidden className="text-lg font-bold text-brand-600">
            {category.name.charAt(0).toUpperCase()}
          </span>
        )}
      </motion.span>
      <span className="max-w-[60px] truncate text-[0.6875rem] font-medium text-white drop-shadow-sm">
        {category.name}
      </span>
    </Link>
  );
}
