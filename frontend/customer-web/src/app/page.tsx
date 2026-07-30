import { CategoryTiles } from "@/components/CategoryTiles";
import { HeroBanner } from "@/components/HeroBanner";
import { TrustMarkers } from "@/components/TrustMarkers";

export default function Home() {
  return (
    <main className="mx-auto flex w-full max-w-6xl flex-col gap-12 px-6 py-10">
      <HeroBanner />

      <section aria-labelledby="categories-heading" className="flex flex-col gap-5">
        <h2 id="categories-heading" className="text-xl font-semibold tracking-tight">
          Popular categories
        </h2>
        <CategoryTiles />
      </section>

      <TrustMarkers />
    </main>
  );
}
