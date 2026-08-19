"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { Alert, Button, Modal, Skeleton, cx } from "@/components/ui";
import { useSelectedCity } from "@/hooks/useSelectedCity";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import { clearSelectedLocality, setSelectedCity, setSelectedLocality } from "@/lib/location";
import type { City, LocalitySearchResult } from "@/lib/types";

/**
 * City picker (SRS 11.1, 11.4.1): a trigger button that opens a modal list of
 * serviceable cities. Selecting one persists it (lib/location.ts) and every
 * subscribed screen (category tiles, category/service pages) re-fetches
 * against the new city.
 *
 * The dialog is the shared `Modal` rather than a hand-rolled overlay, so it
 * inherits Escape-to-close, focus trapping and focus restore — the previous
 * bespoke version had none of those.
 */
export function CitySelector({ transparent = false }: { transparent?: boolean }) {
  const { city, locality } = useSelectedCity();
  const [open, setOpen] = useState(false);

  // "…" while storage is still being read, "Select city" once read and
  // empty, "City" for a city-only pick, "City - Area" once an area is
  // narrowed too (SRS 11.1.3's "filtered by selected city/serviceability").
  const label =
    city === undefined ? "…" : city === null ? "Select city" : locality ? `${city.name} - ${locality.name}` : city.name;

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        className={cx(
          "inline-flex h-9 max-w-[13rem] items-center gap-1.5 rounded-lg px-3 text-sm font-medium transition-colors duration-fast ease-out",
          transparent
            ? "border border-white/30 bg-white/10 text-white [text-shadow:0_1px_3px_rgb(0_0_0/0.5)] hover:bg-white/20"
            : "border border-line bg-surface text-fg shadow-xs hover:border-line-strong hover:bg-surface-2",
        )}
      >
        <PinIcon className={transparent ? "text-white/80" : undefined} />
        <span className="truncate">{label}</span>
      </button>

      <Modal
        open={open}
        onClose={() => setOpen(false)}
        title="Select your city"
        description="We'll show the services available near you."
        size="sm"
      >
        <div className="flex flex-col gap-5">
          <CityList onPicked={() => setOpen(false)} selectedId={city?.id ?? null} />
          {city ? (
            <AreaList
              city={city}
              selectedLocalityId={locality?.id ?? null}
              onPicked={() => setOpen(false)}
            />
          ) : null}
        </div>
      </Modal>
    </>
  );
}

function CityList({
  onPicked,
  selectedId,
}: {
  onPicked: () => void;
  selectedId: string | null;
}) {
  const query = useQuery({
    queryKey: ["geography", "cities"],
    queryFn: () => apiFetch<City[]>(`${API_V1}/geography/cities`),
  });

  if (query.isPending) {
    return (
      <div className="flex flex-col gap-1.5">
        {Array.from({ length: 5 }, (_, index) => (
          <Skeleton key={index} className="h-11 w-full" />
        ))}
      </div>
    );
  }

  if (query.isError) {
    return (
      <Alert
        tone="error"
        action={
          <Button size="sm" variant="secondary" onClick={() => query.refetch()}>
            Retry
          </Button>
        }
      >
        {describeError(query.error)}
      </Alert>
    );
  }

  if (query.data.length === 0) {
    return (
      <p className="py-6 text-center text-sm text-fg-muted">
        No serviceable cities yet — check back soon.
      </p>
    );
  }

  return (
    <ul className="flex max-h-80 flex-col gap-1 overflow-y-auto">
      {query.data.map((city) => {
        const isSelected = city.id === selectedId;
        return (
          <li key={city.id}>
            <button
              type="button"
              onClick={() => {
                setSelectedCity(city);
                onPicked();
              }}
              aria-current={isSelected ? "true" : undefined}
              className={cx(
                "flex w-full items-center justify-between gap-3 rounded-lg px-3 py-2.5 text-left text-sm transition-colors duration-fast ease-out",
                isSelected
                  ? "bg-brand-50 font-medium text-brand-700 dark:bg-brand-500/15 dark:text-brand-300"
                  : "text-fg hover:bg-surface-2",
              )}
            >
              <span className="min-w-0 truncate">
                {city.name}
                <span className="ml-1.5 text-fg-subtle">· {city.stateName}</span>
              </span>
              {isSelected ? <CheckIcon /> : null}
            </button>
          </li>
        );
      })}
    </ul>
  );
}

/**
 * Areas within the selected city (SRS 11.1.3 - "filtered by selected city/
 * serviceability"): narrows category browsing to one pincode, or clears
 * back to every serviceable area in the city. No search box, unlike the
 * booking-flow `LocalitySelector` - this is a short, browsable "which
 * launched area am I in" list, not a lookup over a customer's own address.
 */
function AreaList({
  city,
  selectedLocalityId,
  onPicked,
}: {
  city: City;
  selectedLocalityId: string | null;
  onPicked: () => void;
}) {
  const query = useQuery({
    queryKey: ["geography", "localities", city.id],
    queryFn: () => apiFetch<LocalitySearchResult[]>(`${API_V1}/geography/cities/${city.id}/localities`),
  });

  if (query.isPending) {
    return (
      <div className="flex flex-col gap-1.5">
        <Skeleton className="h-3 w-24" />
        <Skeleton className="h-11 w-full" />
        <Skeleton className="h-11 w-full" />
      </div>
    );
  }

  // A city can be live with no areas registered yet under it - quietly omit
  // this section rather than showing an empty-looking list under a heading.
  if (query.isError || query.data.length === 0) {
    return null;
  }

  return (
    <div className="flex flex-col gap-1.5">
      <p className="px-1 text-xs font-semibold uppercase tracking-wide text-fg-subtle">
        Areas in {city.name}
      </p>
      <ul className="flex max-h-56 flex-col gap-1 overflow-y-auto">
        <li>
          <button
            type="button"
            onClick={() => {
              clearSelectedLocality();
              onPicked();
            }}
            aria-current={selectedLocalityId === null ? "true" : undefined}
            className={cx(
              "flex w-full items-center justify-between gap-3 rounded-lg px-3 py-2.5 text-left text-sm transition-colors duration-fast ease-out",
              selectedLocalityId === null
                ? "bg-brand-50 font-medium text-brand-700 dark:bg-brand-500/15 dark:text-brand-300"
                : "text-fg hover:bg-surface-2",
            )}
          >
            <span>All of {city.name}</span>
            {selectedLocalityId === null ? <CheckIcon /> : null}
          </button>
        </li>
        {query.data.map((locality) => {
          const isSelected = locality.id === selectedLocalityId;
          return (
            <li key={locality.id}>
              <button
                type="button"
                onClick={() => {
                  setSelectedLocality({ id: locality.id, name: locality.name, pincodeId: locality.pincodeId });
                  onPicked();
                }}
                aria-current={isSelected ? "true" : undefined}
                className={cx(
                  "flex w-full items-center justify-between gap-3 rounded-lg px-3 py-2.5 text-left text-sm transition-colors duration-fast ease-out",
                  isSelected
                    ? "bg-brand-50 font-medium text-brand-700 dark:bg-brand-500/15 dark:text-brand-300"
                    : "text-fg hover:bg-surface-2",
                )}
              >
                <span className="min-w-0 truncate">
                  {locality.name}
                  <span className="ml-1.5 text-fg-subtle">· {locality.pincodeCode}</span>
                </span>
                {isSelected ? <CheckIcon /> : null}
              </button>
            </li>
          );
        })}
      </ul>
    </div>
  );
}

function PinIcon({ className }: { className?: string }) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.75"
      strokeLinecap="round"
      strokeLinejoin="round"
      className={cx("h-4 w-4 shrink-0", className ?? "text-fg-subtle")}
      aria-hidden
    >
      <path d="M12 21s7-6.4 7-11a7 7 0 1 0-14 0c0 4.6 7 11 7 11Z" />
      <circle cx="12" cy="10" r="2.5" />
    </svg>
  );
}

function CheckIcon() {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2.25"
      strokeLinecap="round"
      strokeLinejoin="round"
      className="h-4 w-4 shrink-0"
      aria-hidden
    >
      <path d="m5 13 4 4L19 7" />
    </svg>
  );
}
