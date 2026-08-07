"use client";

import Link from "next/link";
import { motion } from "motion/react";
import type { MouseEventHandler, ReactNode } from "react";
import type { Variants } from "motion/react";

/**
 * Shared spring for every tactile hover/press micro-interaction (cards,
 * tiles, buttons) — one physical feel across the product, same spirit as
 * globals.css's single `--ease-out` curve for CSS transitions.
 */
export const SPRING = { type: "spring", stiffness: 420, damping: 32 } as const;

const container: Variants = {
  hidden: {},
  show: { transition: { staggerChildren: 0.07, delayChildren: 0.03 } },
};

const item: Variants = {
  hidden: { opacity: 0, y: 14 },
  show: { opacity: 1, y: 0, transition: { duration: 0.5, ease: [0.16, 1, 0.3, 1] } },
};

/**
 * Scroll-triggered staggered entrance for a grid/list. Fires once when ~15%
 * into view, not on every re-scroll. Reduced-motion is handled once, product
 * -wide, by the `MotionConfig reducedMotion="user"` in app/providers.tsx.
 *
 * `as="ul"` keeps real list semantics for screen readers when wrapping a
 * list of bookings/addresses/etc; use `RevealItem` (an `motion.li`) for its
 * children in that case instead of a bare `motion.div`.
 */
export function Reveal({
  children,
  className,
  as = "div",
}: {
  children: ReactNode;
  className?: string;
  as?: "div" | "ul";
}) {
  const MotionTag = as === "ul" ? motion.ul : motion.div;
  return (
    <MotionTag
      className={className}
      variants={container}
      initial="hidden"
      whileInView="show"
      viewport={{ once: true, amount: 0.15 }}
    >
      {children}
    </MotionTag>
  );
}

/** Apply to each direct child of a `Reveal` to pick up its stagger slot. */
export const revealItem = item;

/** `motion.li` counterpart to `revealItem`, for use inside `<Reveal as="ul">`. */
export function RevealItem({ children, className }: { children: ReactNode; className?: string }) {
  return (
    <motion.li variants={item} className={className}>
      {children}
    </motion.li>
  );
}

/**
 * `next/link` wrapped for the product's one tactile hover/press feel.
 * `lift` (default) is for grid tiles/cards with room to rise. `nudge` is for
 * full-width stacked list rows, where a vertical lift would visually collide
 * with the row above/below — those get a small rightward shift instead,
 * reading as "go" rather than "float". Color/border/shadow crossfades stay on
 * the CSS `transition` classes passed in via `className`; this only owns the
 * spring-driven transform.
 */
export function MotionLink({
  href,
  className,
  children,
  variant = "lift",
  onClick,
}: {
  href: string;
  className?: string;
  children: ReactNode;
  variant?: "lift" | "nudge";
  onClick?: MouseEventHandler<HTMLAnchorElement>;
}) {
  return (
    <Link href={href} className="group block" onClick={onClick}>
      <motion.div
        whileHover={variant === "lift" ? { y: -5 } : { x: 3 }}
        whileTap={{ scale: variant === "lift" ? 0.97 : 0.99 }}
        transition={SPRING}
        className={className}
      >
        {children}
      </motion.div>
    </Link>
  );
}
