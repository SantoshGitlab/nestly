"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { CitySelector } from "@/components/CitySelector";
import { LocalitySelector } from "@/components/LocalitySelector";
import { Alert } from "@/components/ui";
import { useSelectedCity } from "@/hooks/useSelectedCity";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import { clearSelectedLocality } from "@/lib/location";
import type { ServiceabilityResult, SlotAvailability } from "@/lib/types";

/**
 * Today's local calendar date as YYYY-MM-DD.
 *
 * Built from local date parts, not `toISOString().slice(0, 10)`: toISOString
 * converts to UTC first, so in any timezone ahead of UTC the early hours read
 * as the previous day. In IST (UTC+05:30) that returned yesterday for every
 * customer checking availability before 05:30. Mirrors SlotPicker's isoDate.
 */
function todayIsoDate(): string {
  const now = new Date();
  const year = now.getFullYear();
  const month = `${now.getMonth() + 1}`.padStart(2, "0");
  const day = `${now.getDate()}`.padStart(2, "0");
  return `${year}-${month}-${day}`;
}

/**
 * Serviceability + slot availability at the customer's location (SRS 11.4,
 * 24.4). Blocks browsing further into a booking flow when the service isn't
 * offered there (SRS 11.4.3), rather than only discovering that at checkout.
 *
 * Booking itself is out of scope here: there is no booking/cart API yet
 * (Phase 3+), so a confirmed slot is shown as informational availability,
 * not wired to a submit action that would go nowhere.
 */
export function ServiceAvailability({ serviceId }: { serviceId: string }) {
  const { city, locality } = useSelectedCity();

  if (city === undefined) {
    return null;
  }

  return (
    <section
      aria-labelledby="availability-heading"
      className="flex flex-col gap-3 rounded-xl border border-black/10 bg-white p-5 dark:border-white/15 dark:bg-neutral-900"
    >
      <h2 id="availability-heading" className="text-sm font-semibold uppercase tracking-wide text-neutral-500">
        Availability at your location
      </h2>

      {city === null ? (
        <div className="flex flex-col items-start gap-2">
          <p className="text-sm text-neutral-600 dark:text-neutral-400">
            Select your city to check availability.
          </p>
          <CitySelector />
        </div>
      ) : locality === null ? (
        <LocalitySelector cityId={city.id} />
      ) : (
        <ServiceLocalityAvailability serviceId={serviceId} localityId={locality.id} localityName={locality.name} />
      )}
    </section>
  );
}

function ServiceLocalityAvailability({
  serviceId,
  localityId,
  localityName,
}: {
  serviceId: string;
  localityId: string;
  localityName: string;
}) {
  const serviceabilityQuery = useQuery({
    queryKey: ["serviceability", "service", serviceId, localityId],
    queryFn: () =>
      apiFetch<ServiceabilityResult>(`${API_V1}/serviceability/services/${serviceId}?localityId=${localityId}`),
  });

  return (
    <div className="flex flex-col gap-3">
      <div className="flex items-center justify-between gap-3 text-sm">
        <p className="text-neutral-600 dark:text-neutral-400">
          Checking for <span className="font-medium text-inherit">{localityName}</span>
        </p>
        <button type="button" onClick={clearSelectedLocality} className="font-medium hover:underline">
          Change
        </button>
      </div>

      {serviceabilityQuery.isPending ? (
        <p className="text-sm text-neutral-500">Checking availability…</p>
      ) : serviceabilityQuery.isError ? (
        <Alert>{describeError(serviceabilityQuery.error)}</Alert>
      ) : !serviceabilityQuery.data.isServiceable ? (
        <Alert tone="error">This service isn&apos;t available in your area yet.</Alert>
      ) : (
        <SlotPreview serviceId={serviceId} localityId={localityId} />
      )}
    </div>
  );
}

function SlotPreview({ serviceId, localityId }: { serviceId: string; localityId: string }) {
  const [date] = useState(todayIsoDate);

  const query = useQuery({
    queryKey: ["slots", serviceId, localityId, date],
    queryFn: () =>
      apiFetch<SlotAvailability>(
        `${API_V1}/slots?serviceId=${serviceId}&localityId=${localityId}&date=${date}`,
      ),
  });

  if (query.isPending) {
    return <p className="text-sm text-neutral-500">Loading slots…</p>;
  }

  if (query.isError) {
    return <Alert>{describeError(query.error)}</Alert>;
  }

  if (query.data.slots.length === 0) {
    return <p className="text-sm text-neutral-500">No slots available today. Try another date at checkout.</p>;
  }

  return (
    <div className="flex flex-wrap gap-2">
      {query.data.slots.map((slot) => (
        <span
          key={slot.slotWindowId}
          className="rounded-full border border-black/15 px-3 py-1 text-xs dark:border-white/20"
        >
          {slot.name} · {slot.startTime.slice(0, 5)}–{slot.endTime.slice(0, 5)}
        </span>
      ))}
    </div>
  );
}
