import Link from "next/link";
import type { CategorySummary } from "@/lib/types";

/** Category card for a listing/tile grid (SRS 11.1.2, 11.5.1). */
export function CategoryTile({ category }: { category: CategorySummary }) {
  return (
    <Link
      href={`/categories/${category.slug}`}
      className="group flex flex-col items-center gap-3 rounded-xl border border-black/10 bg-white p-5 text-center transition-colors hover:border-black/25 dark:border-white/15 dark:bg-neutral-900 dark:hover:border-white/30"
    >
      <span
        aria-hidden="true"
        className="flex h-14 w-14 items-center justify-center rounded-full bg-black/5 text-2xl dark:bg-white/10"
      >
        {category.iconUrl ? (
          // eslint-disable-next-line @next/next/no-img-element -- category icons are admin-supplied, arbitrary external URLs unsuited to static optimization.
          <img src={category.iconUrl} alt="" className="h-8 w-8 object-contain" />
        ) : (
          "🧰"
        )}
      </span>
      <span className="text-sm font-medium group-hover:underline">{category.name}</span>
      {category.isFeatured ? (
        <span className="rounded-full bg-black/5 px-2 py-0.5 text-xs text-neutral-600 dark:bg-white/10 dark:text-neutral-400">
          Popular
        </span>
      ) : null}
    </Link>
  );
}
