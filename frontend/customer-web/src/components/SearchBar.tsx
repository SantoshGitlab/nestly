"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import type { FormEvent } from "react";

/** Global search entry point (SRS 11.1.3 - "search should be available globally from homepage"). */
export function SearchBar() {
  const router = useRouter();
  const [query, setQuery] = useState("");

  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
    const trimmed = query.trim();
    if (trimmed.length < 2) return;
    router.push(`/search?q=${encodeURIComponent(trimmed)}`);
  };

  return (
    <form onSubmit={onSubmit} role="search" className="flex gap-2">
      <label htmlFor="catalog-search" className="sr-only">
        Search services and categories
      </label>
      <input
        id="catalog-search"
        type="search"
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        placeholder="Search for a service, e.g. &quot;AC repair&quot;"
        className="w-full rounded-lg border border-black/15 bg-white px-4 py-2.5 text-sm text-black outline-none focus:border-black focus:ring-1 focus:ring-black dark:border-white/20 dark:bg-white/10 dark:text-inherit dark:focus:border-white dark:focus:ring-white"
      />
      <button
        type="submit"
        className="shrink-0 rounded-lg bg-white px-4 py-2.5 text-sm font-medium text-black hover:bg-neutral-200 dark:bg-black dark:text-white dark:hover:bg-neutral-800"
      >
        Search
      </button>
    </form>
  );
}
