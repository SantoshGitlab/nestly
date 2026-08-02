import Link from "next/link";
import { CategoryTiles } from "@/components/CategoryTiles";
import { HeroBanner } from "@/components/HeroBanner";
import { TrustMarkers } from "@/components/TrustMarkers";

export default function Home() {
  return (
    <main className="mx-auto flex w-full max-w-6xl flex-col gap-14 px-4 py-8 sm:px-6 sm:py-12">
      <HeroBanner />

      <section aria-labelledby="categories-heading" className="flex flex-col gap-6">
        <div className="flex items-end justify-between gap-4">
          <div>
            <h2 id="categories-heading" className="text-xl font-semibold tracking-tight text-fg">
              Popular categories
            </h2>
            <p className="mt-1 text-sm text-fg-muted">
              Everything your home needs, from one place.
            </p>
          </div>
          <Link
            href="/categories"
            className="shrink-0 text-sm font-medium text-brand-600 underline-offset-4 hover:underline dark:text-brand-400"
          >
            Browse all
          </Link>
        </div>
        <CategoryTiles />
      </section>

      <section aria-labelledby="why-heading" className="flex flex-col gap-6">
        <h2 id="why-heading" className="text-xl font-semibold tracking-tight text-fg">
          Why book with Nestly
        </h2>
        <TrustMarkers />
      </section>
    </main>
  );
}
