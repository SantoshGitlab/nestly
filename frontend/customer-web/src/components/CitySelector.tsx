"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { useSelectedCity } from "@/hooks/useSelectedCity";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import { setSelectedCity } from "@/lib/location";
import type { City } from "@/lib/types";

/**
 * City picker (SRS 11.1, 11.4.1): a trigger button that opens a modal list of
 * serviceable cities. Selecting one persists it (lib/location.ts) and every
 * subscribed screen (category tiles, category/service pages) re-fetches
 * against the new city.
 */
export function CitySelector() {
  const { city } = useSelectedCity();
  const [open, setOpen] = useState(false);

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="flex items-center gap-1.5 rounded-lg border border-black/15 px-3 py-1.5 text-sm font-medium hover:bg-black/5 dark:border-white/20 dark:hover:bg-white/10"
      >
        <span aria-hidden="true">📍</span>
        {city === undefined ? "…" : (city?.name ?? "Select city")}
      </button>

      {open ? <CityPickerModal onClose={() => setOpen(false)} /> : null}
    </>
  );
}

function CityPickerModal({ onClose }: { onClose: () => void }) {
  const query = useQuery({
    queryKey: ["geography", "cities"],
    queryFn: () => apiFetch<City[]>(`${API_V1}/geography/cities`),
  });

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-label="Select your city"
      className="fixed inset-0 z-50 flex items-start justify-center bg-black/40 px-4 pt-24"
      onClick={onClose}
    >
      <div
        className="w-full max-w-sm rounded-xl bg-white p-5 shadow-lg dark:bg-neutral-900"
        onClick={(e) => e.stopPropagation()}
      >
        <div className="mb-4 flex items-center justify-between">
          <h2 className="text-lg font-semibold">Select your city</h2>
          <button
            type="button"
            onClick={onClose}
            aria-label="Close"
            className="rounded p-1 text-neutral-500 hover:bg-black/5 dark:hover:bg-white/10"
          >
            ✕
          </button>
        </div>

        {query.isPending ? (
          <p className="text-sm text-neutral-500">Loading cities…</p>
        ) : query.isError ? (
          <p className="text-sm text-red-600 dark:text-red-400">{describeError(query.error)}</p>
        ) : query.data.length === 0 ? (
          <p className="text-sm text-neutral-500">No serviceable cities yet.</p>
        ) : (
          <ul className="flex max-h-80 flex-col gap-1 overflow-y-auto">
            {query.data.map((city) => (
              <li key={city.id}>
                <button
                  type="button"
                  onClick={() => {
                    setSelectedCity(city);
                    onClose();
                  }}
                  className="w-full rounded-lg px-3 py-2 text-left text-sm hover:bg-black/5 dark:hover:bg-white/10"
                >
                  {city.name}
                  <span className="ml-1 text-neutral-500">· {city.stateName}</span>
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>
    </div>
  );
}
