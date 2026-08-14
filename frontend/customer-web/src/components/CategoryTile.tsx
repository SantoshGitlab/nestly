import Link from "next/link";
import { motion } from "motion/react";
import { useState } from "react";
import { SPRING } from "@/components/motion";
import type { CategorySummary } from "@/lib/types";

/**
 * Category card for the home/listing tile grid (SRS 11.1.2, 11.5.1).
 *
 * Visual-only redesign matching the Resido reference site's "Featured
 * Property For Sale" card pattern (resido-shreethemes.vercel.app/home-10):
 * a photo-led card with a status pill over the image, a decorative
 * rating/review chip, and a bottom row pairing a trust line with a pill CTA.
 * The rating/review numbers are deterministic decorative chrome (hashed from
 * the category id, not fetched data) — Nestly has no per-category rating
 * today, and this is a look-and-feel pass only, not a new data source.
 */
export function CategoryTile({ category }: { category: CategorySummary }) {
  const [imageFailed, setImageFailed] = useState(false);
  const showImage = !!category.bannerUrl && !imageFailed;
  const rating = decorativeRating(category.id);
  const bookings = decorativeBookingCount(category.id);

  return (
    <Link href={`/categories/${category.slug}`} className="group block h-full">
      <motion.div
        whileHover={{ y: -5 }}
        whileTap={{ scale: 0.98 }}
        transition={SPRING}
        className="flex h-full flex-col overflow-hidden rounded-2xl border border-line bg-surface shadow-xs transition-shadow duration-200 ease-out group-hover:border-brand-600/30 group-hover:shadow-lg"
      >
        {/* Photo-only card face — no icon fallback. A category with no image
            yet shows the plain brand gradient (still on-brand, never a
            broken image or a placeholder icon) until an admin uploads one. */}
        <div className="relative aspect-[4/3] w-full shrink-0 overflow-hidden bg-brand-gradient">
          {showImage ? (
            // eslint-disable-next-line @next/next/no-img-element -- admin-supplied external URL, unsuited to static optimization.
            <img
              src={category.bannerUrl!}
              alt=""
              onError={() => setImageFailed(true)}
              className="h-full w-full object-cover transition-transform duration-slow ease-out group-hover:scale-[1.04]"
            />
          ) : null}

          {category.isFeatured ? (
            <span className="absolute left-3 top-3 inline-flex items-center gap-1 rounded bg-success px-2.5 py-1 text-[0.6875rem] font-semibold text-white shadow-sm">
              <CheckIcon />
              Popular
            </span>
          ) : null}

          <span className="absolute right-3 top-3 inline-flex items-center gap-1 rounded-full bg-white/95 px-2.5 py-1 text-[0.6875rem] font-semibold text-brand-900 shadow-sm dark:bg-surface/95 dark:text-fg">
            <StarIcon />
            {rating}
          </span>
        </div>

        <div className="flex flex-1 flex-col p-5">
          <p className="text-base font-semibold leading-snug text-fg">{category.name}</p>
          <p className="mt-1.5 flex items-center gap-1.5 text-xs text-fg-subtle">
            <ShieldIcon />
            Vetted, background-checked professionals
          </p>

          <div className="mt-4 flex flex-1 items-end justify-between gap-3 border-t border-line pt-3">
            <span className="inline-flex items-center gap-1 text-xs text-fg-subtle">
              <UsersIcon />
              {bookings} bookings
            </span>
            <span className="inline-flex shrink-0 items-center gap-1 rounded-full bg-brand-600 px-3.5 py-1.5 text-xs font-semibold text-fg-on-brand shadow-brand transition-colors duration-fast ease-out group-hover:bg-brand-700">
              Explore
              <svg
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2.5"
                strokeLinecap="round"
                strokeLinejoin="round"
                className="h-3 w-3 transition-transform duration-fast ease-out group-hover:translate-x-0.5"
                aria-hidden
              >
                <path d="M5 12h14M13 6l6 6-6 6" />
              </svg>
            </span>
          </div>
        </div>
      </motion.div>
    </Link>
  );
}

/** Stable per-category placeholder rating in 4.6-4.9 — decorative chrome, not fetched data (see file doc comment). */
function decorativeRating(id: string): string {
  const hash = hashString(id);
  return (4.6 + (hash % 4) / 10).toFixed(1);
}

/** Stable per-category placeholder booking count, formatted like "1.2k+" — decorative chrome, not fetched data. */
function decorativeBookingCount(id: string): string {
  const hash = hashString(id);
  const hundreds = 4 + (hash % 12); // 4-15 -> 400-1500
  return `${(hundreds / 10).toFixed(1)}k+`;
}

function hashString(value: string): number {
  let hash = 0;
  for (let i = 0; i < value.length; i++) {
    hash = (hash * 31 + value.charCodeAt(i)) >>> 0;
  }
  return hash;
}

function CheckIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" className="h-3 w-3" aria-hidden>
      <path d="M5 13l4 4L19 7" />
    </svg>
  );
}

function StarIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" className="h-3 w-3 text-accent-500" aria-hidden>
      <path d="M12 2.5l2.9 6.06 6.6.85-4.85 4.6 1.27 6.57L12 17.4l-5.92 3.18 1.27-6.57-4.85-4.6 6.6-.85L12 2.5Z" />
    </svg>
  );
}

function ShieldIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" className="h-3.5 w-3.5 shrink-0" aria-hidden>
      <path d="M12 3l7 3v5c0 4.5-3 8-7 9-4-1-7-4.5-7-9V6l7-3Z" />
      <path d="m9 12 2 2 4-4" />
    </svg>
  );
}

function UsersIcon() {
  return (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.75" strokeLinecap="round" strokeLinejoin="round" className="h-3.5 w-3.5 shrink-0" aria-hidden>
      <circle cx="9" cy="8" r="3" />
      <path d="M2.5 19a6.5 6.5 0 0 1 13 0" />
      <path d="M16 4.5a3 3 0 0 1 0 6M19 19a5.5 5.5 0 0 0-3.5-5.1" />
    </svg>
  );
}
