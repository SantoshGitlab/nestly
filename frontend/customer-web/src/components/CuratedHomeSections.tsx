"use client";

import { useQuery } from "@tanstack/react-query";
import { motion } from "motion/react";
import { CategoryLandingSections } from "@/components/CategoryLandingSections";
import { MostBookedSection } from "@/components/MostBookedSection";
import { Reveal, revealItem } from "@/components/motion";
import { SubCategoryTile } from "@/components/SubCategoryTile";
import { getHomeLanding } from "@/lib/landing-api";

/**
 * The three admin-curated home-page blocks, in the order SRS specifies:
 * New & Trending, then Most Booked Services, then the per-category strips.
 * One request for all three (`GET /landing/home`) rather than three separate
 * queries, so the page never shows one section ready and the others still
 * loading.
 *
 * Silent while loading and on error/empty: unlike the hero or the trust
 * markers, nothing here is load-bearing for the page - an admin who has not
 * configured a section yet should get a home page that simply omits it, not
 * a skeleton or an error banner for content that was never going to exist.
 */
export function CuratedHomeSections() {
  const query = useQuery({ queryKey: ["home-landing"], queryFn: getHomeLanding });

  if (!query.data) {
    return null;
  }

  const { newAndTrending, mostBooked, categorySections } = query.data;

  return (
    <div className="flex flex-col gap-14">
      {newAndTrending.length > 0 ? (
        <section aria-labelledby="new-and-trending-heading" className="flex flex-col gap-6">
          <h2 id="new-and-trending-heading" className="text-display-sm font-bold tracking-tight text-fg">
            New &amp; Trending
          </h2>
          <Reveal className="flex flex-wrap justify-center gap-5 sm:justify-start">
            {newAndTrending.map((category) => (
              <motion.div key={category.id} variants={revealItem}>
                <SubCategoryTile category={category} />
              </motion.div>
            ))}
          </Reveal>
        </section>
      ) : null}

      <MostBookedSection services={mostBooked} />

      <CategoryLandingSections sections={categorySections} />
    </div>
  );
}
