"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { Alert, Button, Modal, Skeleton, cx } from "@/components/ui";
import { useSelectedCity } from "@/hooks/useSelectedCity";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import { setSelectedCity } from "@/lib/location";
import type { City } from "@/lib/types";

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
export function CitySelector() {
  const { city } = useSelectedCity();
  const [open, setOpen] = useState(false);

  return (
    <>
      <button
        type="button"
        onClick={() => setOpen(true)}
        className="inline-flex h-9 max-w-[11rem] items-center gap-1.5 rounded-lg border border-line bg-surface px-3 text-sm font-medium text-fg shadow-xs transition-colors duration-fast ease-out hover:border-line-strong hover:bg-surface-2"
      >
        <PinIcon />
        <span className="truncate">
          {/* `undefined` is "still reading storage", `null` is "read, none chosen" —
              they must not collapse into the same label. */}
          {city === undefined ? "…" : (city?.name ?? "Select city")}
        </span>
      </button>

      <Modal
        open={open}
        onClose={() => setOpen(false)}
        title="Select your city"
        description="We'll show the services available near you."
        size="sm"
      >
        <CityList onPicked={() => setOpen(false)} selectedId={city?.id ?? null} />
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

function PinIcon() {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.75"
      strokeLinecap="round"
      strokeLinejoin="round"
      className="h-4 w-4 shrink-0 text-fg-subtle"
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
