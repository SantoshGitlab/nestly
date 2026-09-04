"use client";

import Link from "next/link";
import { motion } from "motion/react";
import { useState } from "react";
import { SPRING } from "@/components/motion";
import type { LandingSubCategory } from "@/lib/landing-types";

/**
 * Sub-category card for "New & Trending" - a square image and the
 * "Category → Sub-category" name only, deliberately no price (this links
 * into a category, not one bookable service). Shares `CategoryTile`'s visual
 * language (rounded-2xl surface, border-line, shadow-xs, the same hover
 * lift/tap spring) without its decorative rating/booking-count chrome, which
 * belongs to the full category browsing grid, not this compact strip.
 *
 * Sized by its grid cell (`aspect-[4/3] w-full`), not a fixed 233px box: a
 * pixel-fixed card in a wrapping row leaves a lopsided gap the moment the
 * admin-picked item count doesn't divide evenly into a row - worse on wider
 * screens that fit more per row. Stretching to the column width means every
 * row fills edge-to-edge regardless of item count or viewport. The 4:3 ratio
 * itself matches `ServiceCard`/`CategoryTile` - every image-fronted listing
 * card site-wide shares one photo shape, not a mix of square and 4:3.
 */
export function SubCategoryTile({ category }: { category: LandingSubCategory }) {
  const [imageFailed, setImageFailed] = useState(false);
  const showImage = !!category.imageUrl && !imageFailed;

  return (
    <Link href={`/categories/${category.slug}`} className="group block">
      <motion.div
        whileHover={{ y: -4 }}
        whileTap={{ scale: 0.98 }}
        transition={SPRING}
        className="overflow-hidden rounded-2xl border border-line bg-surface shadow-xs transition-shadow duration-200 ease-out group-hover:border-brand-600/30 group-hover:shadow-md"
      >
        <div className="relative aspect-[4/3] w-full overflow-hidden bg-brand-gradient">
          {showImage ? (
            // eslint-disable-next-line @next/next/no-img-element -- admin-supplied external URL, unsuited to static optimization.
            <img
              src={category.imageUrl!}
              alt=""
              loading="lazy"
              decoding="async"
              onError={() => setImageFailed(true)}
              className="h-full w-full object-cover transition-transform duration-slow ease-out group-hover:scale-[1.04]"
            />
          ) : null}
        </div>

        <div className="p-4">
          {category.parentCategoryName ? (
            <p className="truncate text-xs text-fg-subtle">{category.parentCategoryName}</p>
          ) : null}
          <p className="truncate text-sm font-semibold leading-snug text-fg">{category.name}</p>
        </div>
      </motion.div>
    </Link>
  );
}
