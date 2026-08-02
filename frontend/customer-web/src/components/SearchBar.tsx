"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import type { FormEvent } from "react";
import { cx } from "@/components/ui";

/** Global search entry point (SRS 11.1.3 - "search should be available globally from homepage"). */
export function SearchBar({
  /** `hero` sits on the brand gradient and needs its own contrast treatment. */
  variant = "default",
  autoFocus = false,
  defaultValue = "",
}: {
  variant?: "default" | "hero";
  autoFocus?: boolean;
  defaultValue?: string;
}) {
  const router = useRouter();
  const [query, setQuery] = useState(defaultValue);
  // The two-character minimum is a real rule, not a suggestion — surface it
  // instead of silently swallowing the submit the way the previous version did.
  const [showMinLengthHint, setShowMinLengthHint] = useState(false);

  const trimmed = query.trim();

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
    if (trimmed.length < 2) {
      setShowMinLengthHint(true);
      return;
    }
    setShowMinLengthHint(false);
    router.push(`/search?q=${encodeURIComponent(trimmed)}`);
  };

  const onHero = variant === "hero";

  return (
    <form onSubmit={onSubmit} role="search" className="w-full">
      <div
        className={cx(
          "flex items-center gap-2 rounded-xl p-1.5 shadow-lg transition duration-fast ease-out focus-within:ring-2",
          onHero
            ? "bg-surface ring-white/40 focus-within:ring-white/70"
            : "border border-line bg-surface shadow-sm focus-within:ring-brand-600/25",
        )}
      >
        <label htmlFor="catalog-search" className="sr-only">
          Search services and categories
        </label>
        <span className="pl-2.5 text-fg-subtle" aria-hidden>
          <svg
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            strokeLinecap="round"
            className="h-[18px] w-[18px]"
          >
            <circle cx="11" cy="11" r="7" />
            <path d="m20 20-3.2-3.2" />
          </svg>
        </span>
        <input
          id="catalog-search"
          type="search"
          value={query}
          autoFocus={autoFocus}
          onChange={(event) => {
            setQuery(event.target.value);
            if (showMinLengthHint) setShowMinLengthHint(false);
          }}
          aria-describedby={showMinLengthHint ? "catalog-search-hint" : undefined}
          placeholder="Search for a service, e.g. &quot;AC repair&quot;"
          className="min-w-0 flex-1 bg-transparent py-2 text-sm text-fg outline-none placeholder:text-fg-subtle"
        />
        <button
          type="submit"
          className="inline-flex h-10 shrink-0 items-center rounded-lg bg-brand-600 px-5 text-sm font-medium text-fg-on-brand transition duration-fast ease-out hover:bg-brand-700 active:scale-[0.98]"
        >
          Search
        </button>
      </div>

      {showMinLengthHint ? (
        <p
          id="catalog-search-hint"
          role="alert"
          className={cx(
            "mt-2 pl-1 text-xs font-medium",
            onHero ? "text-white/90" : "text-danger",
          )}
        >
          Type at least 2 characters to search.
        </p>
      ) : null}
    </form>
  );
}
