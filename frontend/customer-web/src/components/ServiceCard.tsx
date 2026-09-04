"use client";

import Link from "next/link";
import { motion } from "motion/react";
import { useState } from "react";
import { SPRING } from "@/components/motion";
import { getServiceVisual } from "@/lib/serviceVisuals";

/**
 * Service/package card for a listing (SRS 11.5.3): photo, name, starting
 * price - deliberately just those, matching every other listing card
 * site-wide (`SubCategoryTile`/`CategoryTile`'s image + label pattern) so a
 * catalog reads as one consistent system rather than one card style per
 * section. Description/duration/add-ons/an "Explore details" CTA used to
 * render here too, but that's the service's own detail page's job - a
 * listing card's job is to get someone there, not repeat it. Image-forward,
 * matching the photo-driven card pattern used by home-services marketplace
 * apps - `coverImageUrl` is null until an admin sets one (Phase 3 catalog
 * redesign follow-up), in which case a graphic fallback panel renders
 * instead of a broken image. The same fallback also covers a real photo
 * that fails to load (a dead URL, a network hiccup) - `onError` flips to it
 * rather than leaving a browser's broken-image icon in the card.
 */
export function ServiceCard({
  slug,
  name,
  price,
  coverImageUrl,
}: {
  slug: string;
  name: string;
  price: number;
  coverImageUrl?: string | null;
}) {
  const { icon: Icon, gradient } = getServiceVisual(name);
  const [imageFailed, setImageFailed] = useState(false);
  const showImage = coverImageUrl && !imageFailed;

  return (
    <Link href={`/services/${slug}`} className="group block h-full">
      <motion.div
        whileHover={{ y: -5 }}
        whileTap={{ scale: 0.98 }}
        transition={SPRING}
        className="flex h-full flex-col overflow-hidden rounded-2xl border border-line bg-surface shadow-xs transition-shadow duration-200 ease-out group-hover:border-brand-600/30 group-hover:shadow-md"
      >
        <div className="relative aspect-[4/3] w-full shrink-0 overflow-hidden bg-brand-gradient">
          {showImage ? (
            // eslint-disable-next-line @next/next/no-img-element -- admin-supplied external URL, unsuited to static optimization.
            <img
              src={coverImageUrl}
              alt=""
              // These render dozens-deep in a catalog grid, almost all below
              // the fold - eager-loading every one at once is exactly the
              // mobile-network cost docs/FRONTEND.md's RESPONSIVE DESIGN
              // policy calls out.
              loading="lazy"
              decoding="async"
              onError={() => setImageFailed(true)}
              className="h-full w-full object-cover transition-transform duration-slow ease-out group-hover:scale-[1.04]"
            />
          ) : (
            <div
              aria-hidden
              className={`flex h-full w-full items-center justify-center bg-gradient-to-br ${gradient} text-white/90`}
            >
              <Icon />
            </div>
          )}
        </div>

        {/* Just name + price - the same compact text block every other
            listing card uses (`SubCategoryTile`'s label, `CategoryTile`'s
            name row). Everything else this card used to carry (description,
            duration, add-on count, an "Explore details" button) belongs on
            the service's own detail page, one tap away via the card link. */}
        <div className="p-4">
          <p className="truncate text-sm font-semibold leading-snug text-fg">{name}</p>
          <p className="mt-1 text-xs text-fg-subtle">
            Starts at <span className="nums font-medium text-fg">₹{price}</span>
          </p>
        </div>
      </motion.div>
    </Link>
  );
}
