"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useEffect, useState } from "react";
import { CitySelector } from "@/components/CitySelector";
import { LocalitySelector } from "@/components/LocalitySelector";
import { RequireAuth } from "@/components/RequireAuth";
import { SlotPicker } from "@/components/SlotPicker";
import { Alert, Button, Card, Checkbox, PageHeading } from "@/components/ui";
import { useSelectedCity } from "@/hooks/useSelectedCity";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import {
  DAY_OF_WEEK_LABELS,
  RecurringBookingRecurrenceFrequency,
} from "@/lib/types";
import type {
  CreateRecurringBookingPlanRequestBody,
  CustomerAddress,
  RecurringBookingPlanResponse,
  ServiceDetail,
} from "@/lib/types";

const FREQUENCY_OPTIONS: { value: RecurringBookingRecurrenceFrequency; label: string }[] = [
  { value: RecurringBookingRecurrenceFrequency.Weekly, label: "Every week" },
  { value: RecurringBookingRecurrenceFrequency.Biweekly, label: "Every 2 weeks" },
  { value: RecurringBookingRecurrenceFrequency.Monthly, label: "Every month" },
];

/**
 * Set up a recurring booking plan (PRODUCT-ENHANCEMENTS.md section 2, task
 * 187), reached from the booking summary page ("Set up as recurring
 * instead") and from a past booking's detail page ("Set up a recurring
 * booking"). Deliberately mirrors booking/summary/page.tsx's structure
 * (service lookup by slug, address list, SlotPicker) rather than
 * reinventing that flow - the only genuinely new UI is the recurrence
 * section below.
 *
 * The day-of-week (weekly/biweekly) or day-of-month (monthly) the plan
 * recurs on is derived from whichever date the customer picks in the
 * SlotPicker's date strip, rather than asking for it a second time via a
 * separate control - "book it on this day, every week" is the natural
 * reading of picking a Tuesday there.
 */
export default function NewRecurringBookingPlanPage() {
  return (
    <Suspense fallback={<main className="mx-auto w-full max-w-4xl px-6 py-10" />}>
      <RequireAuth>
        <NewRecurringBookingPlanScreen />
      </RequireAuth>
    </Suspense>
  );
}

function NewRecurringBookingPlanScreen() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const serviceSlug = searchParams.get("serviceSlug");
  const { city, locality } = useSelectedCity();

  const [quantity, setQuantity] = useState(1);
  const [selectedAddOnIds, setSelectedAddOnIds] = useState<Set<string>>(new Set());
  const [selectedAddressId, setSelectedAddressId] = useState<string | null>(null);
  const [selectedDate, setSelectedDate] = useState<string>(() => new Date().toISOString().slice(0, 10));
  const [selectedSlotWindowId, setSelectedSlotWindowId] = useState<string | null>(null);
  const [frequency, setFrequency] = useState<RecurringBookingRecurrenceFrequency>(
    RecurringBookingRecurrenceFrequency.Weekly,
  );
  const [occurrenceCount, setOccurrenceCount] = useState<string>("4");
  const [endDate, setEndDate] = useState<string>("");
  const [formError, setFormError] = useState<string | null>(null);

  const serviceQuery = useQuery({
    queryKey: ["service", serviceSlug],
    queryFn: () => apiFetch<ServiceDetail>(`${API_V1}/services/${serviceSlug}`),
    enabled: !!serviceSlug,
  });

  const addressesQuery = useQuery({
    queryKey: ["addresses"],
    queryFn: () => apiFetch<CustomerAddress[]>(`${API_V1}/addresses`, { authenticated: true }),
  });

  useEffect(() => {
    if (selectedAddressId !== null || !addressesQuery.data) return;
    const preferred = addressesQuery.data.find((a) => a.isDefault) ?? addressesQuery.data[0];
    if (preferred) setSelectedAddressId(preferred.id);
  }, [addressesQuery.data, selectedAddressId]);

  const toggleAddOn = (id: string) => {
    setSelectedAddOnIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const service = serviceQuery.data;

  const createMutation = useMutation({
    mutationFn: (body: CreateRecurringBookingPlanRequestBody) =>
      apiFetch<RecurringBookingPlanResponse>(`${API_V1}/recurring-booking-plans`, {
        method: "POST",
        authenticated: true,
        body: JSON.stringify(body),
      }),
    onSuccess: () => router.push("/recurring-bookings"),
  });

  const handleSubmit = () => {
    setFormError(null);

    if (!service || !city || !locality || !selectedAddressId || !selectedSlotWindowId) {
      setFormError("Choose an address and a slot before continuing.");
      return;
    }

    const trimmedCount = occurrenceCount.trim();
    if (!trimmedCount && !endDate) {
      setFormError("Set either a number of visits or an end date, so the plan is bounded.");
      return;
    }

    const anchor = new Date(`${selectedDate}T00:00:00`);
    const isMonthly = frequency === RecurringBookingRecurrenceFrequency.Monthly;

    const body: CreateRecurringBookingPlanRequestBody = {
      serviceId: service.id,
      cityId: city.id,
      addressId: selectedAddressId,
      localityId: locality.id,
      slotWindowId: selectedSlotWindowId,
      quantity,
      frequency,
      recurrenceDayOfWeek: isMonthly ? null : anchor.getDay(),
      recurrenceDayOfMonth: isMonthly ? anchor.getDate() : null,
      startDate: selectedDate,
      endDate: endDate || null,
      occurrenceCount: trimmedCount ? Number(trimmedCount) : null,
      addOns: Array.from(selectedAddOnIds, (addOnId) => ({ addOnId, quantity: 1 })),
    };

    createMutation.mutate(body);
  };

  if (!serviceSlug) {
    return (
      <main className="mx-auto w-full max-w-3xl px-6 py-12">
        <Alert>No service selected. Go back and choose a service to book.</Alert>
      </main>
    );
  }

  if (serviceQuery.isPending) {
    return <main className="mx-auto w-full max-w-3xl px-6 py-12 text-sm text-neutral-500">Loading…</main>;
  }

  if (serviceQuery.isError || !service) {
    return (
      <main className="mx-auto w-full max-w-3xl px-6 py-12">
        <Alert>{describeError(serviceQuery.error)}</Alert>
      </main>
    );
  }

  const dayLabel =
    frequency === RecurringBookingRecurrenceFrequency.Monthly
      ? `day ${new Date(`${selectedDate}T00:00:00`).getDate()} of the month`
      : DAY_OF_WEEK_LABELS[new Date(`${selectedDate}T00:00:00`).getDay()];

  return (
    <main className="mx-auto flex w-full max-w-3xl flex-col gap-6 px-6 py-10">
      <PageHeading title="Set up a recurring booking" subtitle={service.name} />

      <Card title="Service address">
        {addressesQuery.isPending ? (
          <p className="text-sm text-neutral-500">Loading addresses…</p>
        ) : addressesQuery.isError ? (
          <Alert>{describeError(addressesQuery.error)}</Alert>
        ) : addressesQuery.data.length === 0 ? (
          <p className="text-sm text-neutral-600 dark:text-neutral-400">
            You don&apos;t have any saved addresses yet. Add one from the Addresses page first.
          </p>
        ) : (
          <select
            value={selectedAddressId ?? ""}
            onChange={(e) => setSelectedAddressId(e.target.value)}
            className="w-full rounded-lg border border-black/15 bg-transparent px-3 py-2 text-sm outline-none focus:border-black focus:ring-1 focus:ring-black dark:border-white/20 dark:focus:border-white dark:focus:ring-white"
          >
            {addressesQuery.data.map((address) => (
              <option key={address.id} value={address.id}>
                {address.label} - {address.line1}, {address.city} {address.pincode}
              </option>
            ))}
          </select>
        )}
      </Card>

      <Card title="Quantity">
        <input
          type="number"
          min={1}
          value={quantity}
          onChange={(e) => setQuantity(Math.max(1, Number(e.target.value) || 1))}
          className="w-24 rounded-lg border border-black/15 bg-transparent px-3 py-2 text-sm outline-none focus:border-black focus:ring-1 focus:ring-black dark:border-white/20 dark:focus:border-white dark:focus:ring-white"
        />
      </Card>

      {service.addOns.length > 0 ? (
        <Card title="Add-ons">
          <div className="flex flex-col gap-2">
            {service.addOns.map((addOn) => (
              <Checkbox
                key={addOn.id}
                label={`${addOn.name} (+₹${addOn.price})`}
                checked={selectedAddOnIds.has(addOn.id)}
                onChange={() => toggleAddOn(addOn.id)}
              />
            ))}
          </div>
        </Card>
      ) : null}

      <Card title="Slot" description="This day and time window repeats on the schedule you choose below.">
        {city === undefined ? null : city === null ? (
          <div className="flex flex-col items-start gap-2">
            <p className="text-sm text-neutral-600 dark:text-neutral-400">Select your city first.</p>
            <CitySelector />
          </div>
        ) : locality === null ? (
          <LocalitySelector cityId={city.id} />
        ) : (
          <SlotPicker
            serviceId={service.id}
            localityId={locality.id}
            selectedDate={selectedDate}
            onDateChange={setSelectedDate}
            selectedSlotWindowId={selectedSlotWindowId}
            onSlotChange={(id) => setSelectedSlotWindowId(id)}
          />
        )}
      </Card>

      <Card title="Recurrence" description={`Repeats every ${dayLabel.toLowerCase()}.`}>
        <div className="flex flex-col gap-4">
          <div className="flex gap-2">
            {FREQUENCY_OPTIONS.map((option) => (
              <button
                key={option.value}
                type="button"
                aria-pressed={frequency === option.value}
                onClick={() => setFrequency(option.value)}
                className={`rounded-lg border px-3 py-2 text-sm transition-colors ${
                  frequency === option.value
                    ? "border-black bg-black text-white dark:border-white dark:bg-white dark:text-black"
                    : "border-black/15 hover:bg-black/5 dark:border-white/20 dark:hover:bg-white/10"
                }`}
              >
                {option.label}
              </button>
            ))}
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="flex flex-col gap-1.5">
              <label htmlFor="occurrence-count" className="text-sm font-medium">
                Number of visits (optional)
              </label>
              <input
                id="occurrence-count"
                type="number"
                min={1}
                value={occurrenceCount}
                onChange={(e) => setOccurrenceCount(e.target.value)}
                className="rounded-lg border border-black/15 bg-transparent px-3 py-2 text-sm outline-none focus:border-black focus:ring-1 focus:ring-black dark:border-white/20 dark:focus:border-white dark:focus:ring-white"
              />
            </div>
            <div className="flex flex-col gap-1.5">
              <label htmlFor="end-date" className="text-sm font-medium">
                End date (optional)
              </label>
              <input
                id="end-date"
                type="date"
                min={selectedDate}
                value={endDate}
                onChange={(e) => setEndDate(e.target.value)}
                className="rounded-lg border border-black/15 bg-transparent px-3 py-2 text-sm outline-none focus:border-black focus:ring-1 focus:ring-black dark:border-white/20 dark:focus:border-white dark:focus:ring-white"
              />
            </div>
          </div>
          <p className="text-xs text-neutral-500">
            Set at least one so the plan has a definite end - you can always cancel early from the manage screen.
          </p>
        </div>
      </Card>

      {formError ? <Alert>{formError}</Alert> : null}
      {createMutation.isError ? <Alert>{describeError(createMutation.error)}</Alert> : null}

      <div className="flex gap-3">
        <Button type="button" disabled={createMutation.isPending} onClick={handleSubmit}>
          {createMutation.isPending ? "Setting up…" : "Set up recurring booking"}
        </Button>
        <Button type="button" variant="secondary" onClick={() => router.back()}>
          Cancel
        </Button>
      </div>
    </main>
  );
}
