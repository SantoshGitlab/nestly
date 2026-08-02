import Link from "next/link";
import { Badge } from "@/components/ui";
import type { CategorySummary } from "@/lib/types";

/** Category card for a listing/tile grid (SRS 11.1.2, 11.5.1). */
export function CategoryTile({ category }: { category: CategorySummary }) {
  return (
    <Link
      href={`/categories/${category.slug}`}
      className="group relative flex flex-col items-center gap-3 rounded-2xl border border-line bg-surface p-5 text-center shadow-xs transition duration-200 ease-out hover:-translate-y-0.5 hover:border-brand-600/30 hover:shadow-md"
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
    </Link>
  );
}
