import Link from "next/link";

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
    <Link
      href={`/services/${slug}`}
      className="flex flex-col gap-2 rounded-xl border border-black/10 bg-white p-5 transition-colors hover:border-black/25 dark:border-white/15 dark:bg-neutral-900 dark:hover:border-white/30"
    >
      <div className="flex items-start justify-between gap-4">
        <p className="font-medium">{name}</p>
        <p className="shrink-0 text-sm font-semibold">Starts at ₹{price}</p>
      </div>
      <p className="text-sm text-neutral-600 dark:text-neutral-400">{description}</p>
      {addOnCount ? (
        <p className="text-xs text-neutral-500">
          {addOnCount} add-on{addOnCount === 1 ? "" : "s"} available
        </p>
      ) : null}
      <span className="mt-1 text-sm font-medium hover:underline">View details →</span>
    </Link>
  );
}
