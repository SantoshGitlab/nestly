"use client";

import Link from "next/link";
import { motion } from "motion/react";
import { useState } from "react";
import { Reveal, revealItem, SPRING } from "@/components/motion";
import type { CategorySummary } from "@/lib/types";

/**
 * Subcategory picker grid for a category page (e.g. "AC", "Washing Machine"
 * under "AC & Appliance Repair") - an image tile per subcategory, replacing
 * the earlier icon-chip strip. Shares `SubCategoryTile`'s (New & Trending)
 * visual language - rounded-2xl surface, image + name, the same hover lift,
 * the same 4:3 photo ratio as every other listing card site-wide
 * (`ServiceCard`/`CategoryTile`) - but wraps in a responsive grid instead of
 * a fixed 233px card, since this sits inline in a category page's own
 * column width rather than a hero overlay/home-page strip.
 */
export function SubcategoryTileGrid({ subcategories }: { subcategories: CategorySummary[] }) {
  if (subcategories.length === 0) return null;

  return (
    <Reveal className="grid grid-cols-2 gap-5 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5">
      {subcategories.map((subcategory) => (
        <motion.div key={subcategory.id} variants={revealItem}>
          <SubcategoryTile subcategory={subcategory} />
        </motion.div>
      ))}
    </Reveal>
  );
}

function SubcategoryTile({ subcategory }: { subcategory: CategorySummary }) {
  const [imageFailed, setImageFailed] = useState(false);
  const showImage = !!subcategory.bannerUrl && !imageFailed;

  return (
    <Link href={`/categories/${subcategory.slug}`} className="group block">
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
              src={subcategory.bannerUrl!}
              alt=""
              loading="lazy"
              decoding="async"
              onError={() => setImageFailed(true)}
              className="h-full w-full object-cover transition-transform duration-slow ease-out group-hover:scale-[1.04]"
            />
          ) : subcategory.iconUrl ? (
            <div className="flex h-full w-full items-center justify-center p-6">
              {/* eslint-disable-next-line @next/next/no-img-element -- admin-supplied external URL, unsuited to static optimization. */}
              <img src={subcategory.iconUrl} alt="" className="h-full w-full object-contain" />
            </div>
          ) : null}
        </div>

        <div className="p-3">
          <p className="truncate text-sm font-semibold leading-snug text-fg">{subcategory.name}</p>
        </div>
      </motion.div>
    </Link>
  );
}
