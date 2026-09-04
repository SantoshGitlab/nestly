"use client";

import { useQuery } from "@tanstack/react-query";
import { motion } from "motion/react";
import { CategoryLandingSections } from "@/components/CategoryLandingSections";
import { MostBookedSection } from "@/components/MostBookedSection";
import { Reveal, revealItem } from "@/components/motion";
import { SubCategoryTile } from "@/components/SubCategoryTile";
import { Divider } from "@/components/ui";
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

  // Every section here is independently optional (an admin may configure
  // only one of the three), so dividers are placed between whichever ones
  // actually rendered rather than hard-coded between fixed slots - two
  // adjacent hairlines with nothing between them would look like a bug, not
  // a design choice.
  const blocks = [
    newAndTrending.length > 0 ? (
      <section key="new-and-trending" aria-labelledby="new-and-trending-heading" className="flex flex-col gap-6">
        <h2 id="new-and-trending-heading" className="text-display-sm font-bold tracking-tight text-fg">
          New &amp; Trending
        </h2>
        {/* Responsive wrapping grid, not a fixed-233px flex row: percentage-
            wide columns mean every row - including a short last row - fills
            the full container edge to edge, whatever the admin-picked item
            count or viewport width happens to be. (`SubCategoryTile` itself
            stretches to its column via `aspect-[4/3] w-full`.) */}
        <Reveal className="grid grid-cols-2 gap-5 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5">
          {newAndTrending.map((category) => (
            <motion.div key={category.id} variants={revealItem}>
              <SubCategoryTile category={category} />
            </motion.div>
          ))}
        </Reveal>
      </section>
    ) : null,
    mostBooked.length > 0 ? <MostBookedSection key="most-booked" services={mostBooked} /> : null,
    categorySections.length > 0 ? (
      <CategoryLandingSections key="category-sections" sections={categorySections} />
    ) : null,
  ].filter(Boolean);

  return (
    <div className="flex flex-col gap-14">
      {blocks.map((block, index) => (
        <div key={index} className="flex flex-col gap-14">
          {index > 0 ? <Divider /> : null}
          {block}
        </div>
      ))}
    </div>
  );
}
