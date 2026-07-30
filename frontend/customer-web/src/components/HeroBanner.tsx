import { SearchBar } from "@/components/SearchBar";

/**
 * Hero / primary CTA (SRS 11.1.2). Content is static for now: there is no
 * admin-configurable banner backend yet (no Banner/Promotion entity or API
 * anywhere in the catalog module) - SRS 11.1.3's "banner visibility, order,
 * and content shall be admin-configurable" is not implemented server-side,
 * so this deliberately does not fabricate one. Swap this component's content
 * for a data-driven one once that API exists.
 */
export function HeroBanner() {
  return (
    <section className="rounded-2xl bg-gradient-to-br from-black to-neutral-700 px-6 py-14 text-white dark:from-neutral-100 dark:to-white dark:text-black sm:px-10 sm:py-20">
      <h1 className="max-w-2xl text-3xl font-semibold tracking-tight sm:text-4xl">
        Trusted home services, booked in minutes.
      </h1>
      <p className="mt-3 max-w-xl text-white/80 dark:text-black/70">
        Cleaning, repairs, salon, and more - vetted professionals, upfront pricing, and slots that fit your day.
      </p>

      <div className="mt-8 max-w-xl">
        <SearchBar />
      </div>
    </section>
  );
}
