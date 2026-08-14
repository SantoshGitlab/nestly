"use client";

import Image from "next/image";
import { AnimatePresence, motion } from "motion/react";
import { useEffect, useState } from "react";
import { SearchBar } from "@/components/SearchBar";
import { cx } from "@/components/ui";

/**
 * Hero / primary CTA (SRS 11.1.2). Content is static for now: there is no
 * admin-configurable banner backend yet (no Banner/Promotion entity or API
 * anywhere in the catalog module) - SRS 11.1.3's "banner visibility, order,
 * and content shall be admin-configurable" is not implemented server-side,
 * so this deliberately does not fabricate one. Swap this component's content
 * for a data-driven one once that API exists.
 *
 * "Full-bleed slider" v9: true edge-to-edge (no card border/radius, lives
 * outside `app/page.tsx`'s max-w-7xl wrapper — same full-bleed pattern the
 * "Popular categories" band below it already uses) and the legibility
 * treatment is a soft radial tint behind the copy plus a text-shadow on the
 * type itself, not a flat color panel — the photo stays fully visible
 * everywhere the text isn't sitting.
 *
 * `public/images/hero/banner-{1,2,3}.jpg` are pre-cropped (source: licensed
 * stock photography, not AI-generated) to the photography-only two-thirds of
 * each original template graphic — the source files ship with a large blank
 * panel built in for a text-beside-photo layout that no longer applies now
 * that copy sits directly on top of the image; using the raw files here
 * would show that blank panel as dead space across half the banner.
 * `object-[center_25%]` keeps each photo's subject (all three sit in the
 * upper half of the frame) in view when a very wide viewport crops more off
 * the top/bottom than a plain center-crop would.
 *
 * The headline replays a word-by-word entrance every time the slide changes
 * (keyed by `index`, same as the image crossfade) so the caption reads as
 * part of the slide transition, not a static layer sitting on top of it.
 *
 * Auto-advance respects `prefers-reduced-motion`, pauses on hover/focus, and
 * the dots double as manual controls — a slider that only auto-plays with no
 * way to stop it fails WCAG 2.2.2, and this app is otherwise careful about
 * that class of thing (see globals.css's reduced-motion block).
 */
const SLIDES = [
  {
    src: "/images/hero/banner-1.jpg",
    alt: "A Nestly professional cleaning a customer's window",
  },
  {
    src: "/images/hero/banner-2.jpg",
    alt: "A Nestly professional wiping down a customer's kitchen counter",
  },
  {
    src: "/images/hero/banner-3.jpg",
    alt: "A modern kitchen tap, representing Nestly's repair and installation services",
  },
] as const;

const SLIDE_DURATION_MS = 6000;
const HEADLINE = "Trusted home services, booked in minutes.";
const TEXT_SHADOW = { textShadow: "0 1px 3px rgb(0 0 0 / 0.5), 0 4px 20px rgb(0 0 0 / 0.5)" };

export function HeroBanner() {
  const [index, setIndex] = useState(0);
  const [paused, setPaused] = useState(false);

  useEffect(() => {
    if (paused) return;
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;

    const id = setInterval(() => {
      setIndex((current) => (current + 1) % SLIDES.length);
    }, SLIDE_DURATION_MS);
    return () => clearInterval(id);
  }, [paused]);

  return (
    <section
      // Cancels `#main`'s `pt-[4.5rem]` (reserved for `SiteHeader` now being
      // permanently `fixed`) so the banner still starts at true y=0, flush
      // under the header while it's in its transparent-over-photo state.
      className="relative isolate -mt-[4.5rem] w-full overflow-hidden"
      onMouseEnter={() => setPaused(true)}
      onMouseLeave={() => setPaused(false)}
      onFocus={() => setPaused(true)}
      onBlur={() => setPaused(false)}
    >
      {/* `min-h`, not a fixed `h`: the stacked copy (kicker + 3-line headline
          + subtext + search bar) can need more room than these targets on a
          narrow phone where the headline wraps hardest — a fixed height
          either clipped it or forced it to overlap the header above. `min-h`
          keeps the shorter, more balanced target height whenever content
          fits, and only grows on the rare viewport where it doesn't. */}
      <div className="relative min-h-[440px] w-full sm:min-h-[500px] md:min-h-[540px] lg:min-h-[580px]">
        <AnimatePresence initial={false}>
          <motion.div
            key={SLIDES[index].src}
            className="absolute inset-0"
            initial={{ opacity: 0, scale: 1.04 }}
            animate={{ opacity: 1, scale: 1 }}
            exit={{ opacity: 0 }}
            transition={{ opacity: { duration: 1, ease: "easeInOut" }, scale: { duration: 6, ease: "linear" } }}
          >
            <Image
              src={SLIDES[index].src}
              alt={SLIDES[index].alt}
              fill
              priority={index === 0}
              sizes="100vw"
              className="object-cover object-[center_25%]"
            />
          </motion.div>
        </AnimatePresence>

        {/* Soft radial tint behind the copy only — the photo stays fully
            visible at the edges instead of sitting under a flat wash. */}
        <div
          aria-hidden
          className="absolute inset-0 bg-[radial-gradient(62%_58%_at_50%_45%,rgb(0_0_0/0.58),transparent_75%)]"
        />
        {/* Thin top/bottom gradients so the header and dot controls stay legible over a bright sky/wall regardless of slide. */}
        <div aria-hidden className="absolute inset-x-0 top-0 h-24 bg-gradient-to-b from-black/25 to-transparent" />
        <div aria-hidden className="absolute inset-x-0 bottom-0 h-32 bg-gradient-to-t from-black/40 to-transparent" />

        {/* `justify-center` only guards against the header overlap when
            content is short enough for its overflow-around-the-center-point
            to stay clear of the top of the box — measured broken even at
            1440px (kicker landing at y:0, dead center of the header's own
            0-72px band), not just on narrow phones. `justify-start` + a
            top padding matching the header's height makes the overlap
            geometrically impossible at every breakpoint instead of merely
            unlikely at most of them: content always begins right after the
            reserved zone and stacks downward, regardless of how many lines
            the copy wraps to or how much spare height the section has. */}
        <div className="relative z-10 mx-auto flex h-full w-full max-w-3xl flex-col items-center justify-start gap-6 px-5 pb-10 pt-24 text-center sm:px-8">
          <AnimatePresence mode="wait">
            <motion.div
              key={index}
              className="flex flex-col items-center gap-6"
              initial="hidden"
              animate="show"
              exit={{ opacity: 0, y: -8, transition: { duration: 0.3 } }}
              variants={{ hidden: {}, show: { transition: { staggerChildren: 0.05 } } }}
            >
              <motion.p
                variants={fadeUp}
                style={TEXT_SHADOW}
                className="flex items-start justify-center gap-2 text-xs font-semibold uppercase tracking-[0.22em] text-accent-300"
              >
                <ShieldIcon className="mt-0.5 h-4 w-4 shrink-0" />
                <span>Vetted professionals &middot; Upfront pricing</span>
              </motion.p>

              <h1
                style={TEXT_SHADOW}
                className="max-w-2xl text-display-md font-semibold leading-[1.08] text-balance text-white sm:text-display-lg lg:text-display-xl"
              >
                {HEADLINE.split(" ").map((word, i) => (
                  // Margin, not a trailing space character inside the box: a
                  // space as the last text node in an inline-block reliably
                  // collapses at the box edge, which was gluing every word
                  // together ("Trustedhomeservices").
                  <motion.span key={i} variants={fadeUp} className="inline-block mr-[0.28em] last:mr-0">
                    {word}
                  </motion.span>
                ))}
              </h1>

              <motion.p
                variants={fadeUp}
                style={TEXT_SHADOW}
                className="max-w-lg text-[0.9375rem] leading-relaxed text-white/90 text-pretty"
              >
                Cleaning, repairs, salon, and more — background-checked professionals,
                prices you see before you book, and slots that fit your day.
              </motion.p>

              <motion.div variants={fadeUp} className="w-full max-w-lg">
                <SearchBar variant="hero" />
              </motion.div>
            </motion.div>
          </AnimatePresence>
        </div>

        <div className="absolute bottom-6 left-1/2 z-10 flex -translate-x-1/2 items-center gap-2 sm:bottom-8">
          {SLIDES.map((slide, slideIndex) => (
            <button
              key={slide.src}
              type="button"
              onClick={() => setIndex(slideIndex)}
              aria-label={`Show slide ${slideIndex + 1} of ${SLIDES.length}`}
              aria-current={slideIndex === index}
              className={cx(
                "h-1.5 rounded-full transition-all duration-fast ease-out",
                slideIndex === index ? "w-6 bg-white" : "w-1.5 bg-white/45 hover:bg-white/70",
              )}
            />
          ))}
        </div>
      </div>
    </section>
  );
}

const fadeUp = {
  hidden: { opacity: 0, y: 14 },
  show: { opacity: 1, y: 0, transition: { duration: 0.5, ease: "easeOut" } },
};

function ShieldIcon({ className = "h-5 w-5" }: { className?: string }) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.75"
      strokeLinecap="round"
      strokeLinejoin="round"
      className={className}
      aria-hidden
    >
      <path d="M12 3l7 3v5.5c0 4.2-3 8-7 9.5-4-1.5-7-5.3-7-9.5V6l7-3Z" />
      <path d="m9 12 2 2 4-4" />
    </svg>
  );
}
