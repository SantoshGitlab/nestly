import Link from "next/link";
import { motion } from "motion/react";
import { SPRING } from "@/components/motion";

/** Service/package card for a listing (SRS 11.5.3): name, description, starting price, view-detail CTA. */
export function ServiceCard({
  slug,
  name,
  description,
  price,
  addOnCount,
}: {
  slug: string;
  name: string;
  description: string;
  price: number;
  addOnCount?: number;
}) {
  return (
    <Link href={`/services/${slug}`} className="group block h-full">
      <motion.div
        whileHover={{ y: -5 }}
        whileTap={{ scale: 0.98 }}
        transition={SPRING}
        className="flex h-full flex-col rounded-2xl border border-line bg-surface p-5 shadow-xs transition-shadow duration-200 ease-out group-hover:border-brand-600/30 group-hover:shadow-md"
      >
        <div className="flex items-start justify-between gap-4">
          <p className="font-medium leading-snug text-fg">{name}</p>
          <p className="shrink-0 text-right">
            <span className="block text-[0.6875rem] uppercase tracking-wide text-fg-subtle">
              Starts at
            </span>
            <span className="nums text-base font-semibold text-fg">₹{price}</span>
          </p>
        </div>

        {/* Clamped so a long admin-authored description can't make one card in a
            grid twice the height of its neighbours. */}
        <p className="mt-2 line-clamp-2 text-sm leading-relaxed text-fg-muted">{description}</p>

        <div className="mt-4 flex items-center justify-between gap-3 border-t border-line pt-3">
          {addOnCount ? (
            <span className="text-xs text-fg-subtle">
              {addOnCount} add-on{addOnCount === 1 ? "" : "s"} available
            </span>
          ) : (
            <span />
          )}
          <span className="inline-flex items-center gap-1 text-sm font-medium text-brand-600 dark:text-brand-400">
            View details
            <svg
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
              className="h-3.5 w-3.5 transition-transform duration-fast ease-out group-hover:translate-x-0.5"
              aria-hidden
            >
              <path d="M5 12h14M13 6l6 6-6 6" />
            </svg>
          </span>
        </div>
      </motion.div>
    </Link>
  );
}
