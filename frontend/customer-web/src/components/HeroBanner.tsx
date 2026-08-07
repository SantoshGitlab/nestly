"use client";

import { motion } from "motion/react";
import { SearchBar } from "@/components/SearchBar";

/**
 * Hero / primary CTA (SRS 11.1.2). Content is static for now: there is no
 * admin-configurable banner backend yet (no Banner/Promotion entity or API
 * anywhere in the catalog module) - SRS 11.1.3's "banner visibility, order,
 * and content shall be admin-configurable" is not implemented server-side,
 * so this deliberately does not fabricate one. Swap this component's content
 * for a data-driven one once that API exists.
 *
 * The verified-professional cluster on the right is decorative, not a data
 * claim — four categories pulled straight from the subtext copy, not a
 * fabricated stat. It exists to make "vetted professionals" felt rather than
 * just stated, which is the one deliberate flourish on this page; everything
 * else stays quiet by comparison.
 */
export function HeroBanner() {
  return (
    <section className="hero-mesh relative isolate overflow-hidden rounded-3xl px-6 py-14 text-white shadow-xl sm:px-12 sm:py-20">
      {/* Grain overlay: `mix-blend-overlay` lets it read as texture on the
          gradient underneath rather than a visible tiled image. */}
      <div
        aria-hidden
        className="texture-grain pointer-events-none absolute inset-0 -z-10 opacity-[0.05] mix-blend-overlay"
      />

      <div className="grid items-center gap-10 lg:grid-cols-[1fr_auto]">
        {/* CSS-keyframe entrance (`animate-rise`, the same primitive every card/
            modal in the product already uses), not the `motion` library: this
            is the primary conversion path, so it must not depend on JS having
            hydrated and a render loop having ticked to become visible. The
            floating avatar cluster is decorative and can afford that dependency;
            this text and the search box can't. */}
        <div>
          <p
            className="mb-4 inline-flex animate-rise items-center gap-2 rounded-full bg-white/15 px-3 py-1 text-xs font-medium backdrop-blur-sm"
          >
            <span className="h-1.5 w-1.5 rounded-full bg-accent-300" aria-hidden />
            Vetted professionals, upfront pricing
          </p>

          <h1
            style={{ animationDelay: "70ms" }}
            className="max-w-2xl animate-rise text-display-md font-semibold text-balance sm:text-display-lg"
          >
            Trusted home services, booked in minutes.
          </h1>

          <p
            style={{ animationDelay: "140ms" }}
            className="mt-4 max-w-xl animate-rise text-[0.9375rem] leading-relaxed text-white/85 text-pretty"
          >
            Cleaning, repairs, salon, and more — background-checked professionals,
            prices you see before you book, and slots that fit your day.
          </p>

          <div style={{ animationDelay: "210ms" }} className="mt-8 max-w-xl animate-rise">
            <SearchBar variant="hero" />
          </div>
        </div>

        <VerifiedCluster />
      </div>
    </section>
  );
}

const CLUSTER_CATEGORIES = [
  { label: "Cleaning", icon: <BroomIcon /> },
  { label: "Repairs", icon: <WrenchIcon /> },
  { label: "Electrical", icon: <BoltIcon /> },
  { label: "Salon", icon: <ScissorsIcon /> },
] as const;

/** Hidden below `lg`: on a phone this is decoration competing with the CTA for thumb space, not worth the cost. */
function VerifiedCluster() {
  return (
    <div aria-hidden className="hidden lg:block">
      <div className="relative h-56 w-56">
        {CLUSTER_CATEGORIES.map((entry, index) => (
          <motion.div
            key={entry.label}
            className="absolute flex flex-col items-center gap-1.5"
            style={CLUSTER_POSITIONS[index]}
            initial={{ opacity: 0, scale: 0.8 }}
            animate={{
              opacity: 1,
              scale: 1,
              y: [0, -7, 0],
            }}
            transition={{
              opacity: { duration: 0.5, delay: 0.35 + index * 0.08 },
              scale: { duration: 0.5, delay: 0.35 + index * 0.08 },
              y: { duration: 3.4 + index * 0.4, repeat: Infinity, ease: "easeInOut", delay: index * 0.3 },
            }}
          >
            <span className="flex h-14 w-14 items-center justify-center rounded-2xl border border-white/20 bg-white/10 text-white shadow-lg backdrop-blur-md">
              {entry.icon}
            </span>
            <span className="rounded-full bg-white/95 px-2 py-0.5 text-[0.6875rem] font-medium text-brand-900 shadow-sm">
              {entry.label}
            </span>
          </motion.div>
        ))}
      </div>
    </div>
  );
}

const CLUSTER_POSITIONS: React.CSSProperties[] = [
  { left: 0, top: 8 },
  { right: 4, top: 0 },
  { left: 24, bottom: 4 },
  { right: 0, bottom: 20 },
];

const ICON_PROPS = {
  viewBox: "0 0 24 24",
  fill: "none",
  stroke: "currentColor",
  strokeWidth: "1.75",
  strokeLinecap: "round",
  strokeLinejoin: "round",
  className: "h-6 w-6",
} as const;

function BroomIcon() {
  return (
    <svg {...ICON_PROPS}>
      <path d="M19 3 8.5 13.5M13 15l-8 6M9 11l4 4-1.5 3.5L3 20l1.5-8.5L9 11Z" />
    </svg>
  );
}

function WrenchIcon() {
  return (
    <svg {...ICON_PROPS}>
      <path d="M14.7 6.3a4 4 0 0 0-5.4 5.4L4 17l3 3 5.3-5.3a4 4 0 0 0 5.4-5.4l-2.6 2.6-2-2Z" />
    </svg>
  );
}

function BoltIcon() {
  return (
    <svg {...ICON_PROPS}>
      <path d="M13 2 4 14h6l-1 8 9-12h-6l1-8Z" />
    </svg>
  );
}

function ScissorsIcon() {
  return (
    <svg {...ICON_PROPS}>
      <circle cx="6" cy="6" r="2.5" />
      <circle cx="6" cy="18" r="2.5" />
      <path d="m20 4-12 12M8 12l12 8M8.5 8.5 12 12" />
    </svg>
  );
}
