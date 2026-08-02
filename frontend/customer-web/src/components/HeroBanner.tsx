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
    <section className="relative isolate overflow-hidden rounded-3xl bg-brand-gradient px-6 py-14 text-white shadow-lg sm:px-12 sm:py-20">
      {/* Two soft radial washes over the flat gradient. Purely decorative, and
          `isolate` + `-z-10` keep them behind the content without a stacking
          context leaking out of the section. */}
      <div
        aria-hidden
        className="absolute -right-24 -top-24 -z-10 h-80 w-80 rounded-full bg-white/15 blur-3xl"
      />
      <div
        aria-hidden
        className="absolute -bottom-32 -left-20 -z-10 h-80 w-80 rounded-full bg-accent-400/20 blur-3xl"
      />

      <p className="mb-4 inline-flex items-center gap-2 rounded-full bg-white/15 px-3 py-1 text-xs font-medium backdrop-blur-sm">
        <span className="h-1.5 w-1.5 rounded-full bg-accent-300" aria-hidden />
        Vetted professionals, upfront pricing
      </p>

      <h1 className="max-w-2xl text-display-md font-semibold text-balance sm:text-display-lg">
        Trusted home services, booked in minutes.
      </h1>
      <p className="mt-4 max-w-xl text-[0.9375rem] leading-relaxed text-white/85 text-pretty">
        Cleaning, repairs, salon, and more — background-checked professionals,
        prices you see before you book, and slots that fit your day.
      </p>

      <div className="mt-8 max-w-xl">
        <SearchBar variant="hero" />
      </div>
    </section>
  );
}
