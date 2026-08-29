import { CategoryQuickPicks } from "@/components/CategoryQuickPicks";
import { CuratedHomeSections } from "@/components/CuratedHomeSections";
import { HeroBanner } from "@/components/HeroBanner";
import { TrustMarkers } from "@/components/TrustMarkers";

export default function Home() {
  return (
    <main className="flex w-full flex-col gap-14 pb-14 sm:pb-20">
      {/* Full-bleed, edge-to-edge: sits flush under the sticky header with no
          side padding or card chrome, unlike every other section here (which
          is why it's outside the max-w-7xl wrapper below, matching the
          categories band's existing full-bleed pattern). The quick-picks rail
          is a sibling overlay, not a HeroBanner prop - it never touches the
          banner's own CMS-driven slide/carousel logic. */}
      <div className="relative">
        <HeroBanner />
        <div className="pointer-events-none absolute left-0 top-0 z-20 w-full px-5 pt-6 sm:px-8 sm:pt-8">
          <div className="pointer-events-auto mx-auto max-w-3xl sm:mx-0">
            <CategoryQuickPicks />
          </div>
        </div>
      </div>

      <div className="mx-auto w-full max-w-7xl px-4 sm:px-6">
        <CuratedHomeSections />
      </div>

      <div className="mx-auto w-full max-w-7xl px-4 sm:px-6">
        <section aria-labelledby="why-heading" className="flex flex-col gap-6">
          <h2 id="why-heading" className="text-xl font-semibold tracking-tight text-fg">
            Why book with Nestly
          </h2>
          <TrustMarkers />
        </section>
      </div>
    </main>
  );
}
