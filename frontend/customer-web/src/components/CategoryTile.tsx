import Link from "next/link";
import { motion } from "motion/react";
import { Badge } from "@/components/ui";
import { SPRING } from "@/components/motion";
import type { CategorySummary } from "@/lib/types";

/** Category card for a listing/tile grid (SRS 11.1.2, 11.5.1). */
export function CategoryTile({ category }: { category: CategorySummary }) {
  return (
    <Link href={`/categories/${category.slug}`} className="group block">
      <motion.div
        whileHover={{ y: -5 }}
        whileTap={{ scale: 0.97 }}
        transition={SPRING}
        className="relative flex flex-col items-center gap-3 rounded-2xl border border-line bg-surface p-5 text-center shadow-xs transition-shadow duration-200 ease-out group-hover:border-brand-600/30 group-hover:shadow-md"
      >
        {category.isFeatured ? (
          <Badge tone="accent" className="absolute right-2.5 top-2.5">
            Popular
          </Badge>
        ) : null}

        <span
          aria-hidden="true"
          className="flex h-14 w-14 items-center justify-center rounded-2xl bg-brand-50 text-2xl transition-colors duration-200 ease-out group-hover:bg-brand-100 dark:bg-brand-500/15 dark:group-hover:bg-brand-500/25"
        >
          {category.iconUrl ? (
            // eslint-disable-next-line @next/next/no-img-element -- category icons are admin-supplied, arbitrary external URLs unsuited to static optimization.
            <img src={category.iconUrl} alt="" className="h-8 w-8 object-contain" />
          ) : (
            "🧰"
          )}
        </span>

        <span className="text-sm font-medium leading-snug text-fg">{category.name}</span>
      </motion.div>
    </Link>
  );
}
