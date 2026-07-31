"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useEffect, useState } from "react";
import { CitySelector } from "@/components/CitySelector";
import { LocalitySelector } from "@/components/LocalitySelector";
import { RequireAuth } from "@/components/RequireAuth";
import { SlotPicker } from "@/components/SlotPicker";
import { Alert, Button, Card, PageHeading } from "@/components/ui";
import { useSelectedCity } from "@/hooks/useSelectedCity";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import type {
  BookingSummary,
  BookingSummaryRequestBody,
  CustomerAddress,
  PriceBreakdown,
  ServiceDetail,
  SlotRevalidation,
} from "@/lib/types";

/**
 * Booking summary / cart page (SRS 11.7, tasks 62a-f) with slot selection
 * folded in (SRS 11.8, tasks 63a-c) - both feed the same
 * BookingSummaryRequest, so splitting them into separate routes would just
 * mean passing the same half-built request back and forth through the URL.
 *
 * Single-service cart model only (SRS 11.7.1): the request always describes
 * exactly one service, matching BookingSummaryRequest's shape.
 *
 * Wrapped in Suspense: useSearchParams opts the tree below it out of static
 * rendering, and Next's App Router requires a Suspense boundary around that
 * or the production build fails (see search/page.tsx for the same pattern).
 */
export default function BookingSummaryPage() {
  return (
    <Suspense fallback={<main className="mx-auto w-full max-w-4xl px-6 py-10" />}>
      <RequireAuth>
        <BookingSummaryScreen />
      </RequireAuth>
    </Suspense>
  );
}

function BookingSummaryScreen() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const serviceSlug = searchParams.get("serviceSlug");
  const { city, locality } = useSelectedCity();

  const [quantity, setQuantity] = useState(1);
  const [selectedAddOnIds, setSelectedAddOnIds] = useState<Set<string>>(new Set());
  const [selectedAddressId, setSelectedAddressId] = useState<string | null>(null);
  const [selectedDate, setSelectedDate] = useState<string>(() => new Date().toISOString().slice(0, 10));
  const [selectedSlotWindowId, setSelectedSlotWindowId] = useState<string | null>(null);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Coupon (task 77, SRS 11.10.3). appliedCouponCode is the code the backend
  // has confirmed - couponInput is just the text box's draft value, kept
  // separate so a typo mid-edit doesn't silently change what's charged.
  const [couponInput, setCouponInput] = useState("");
  const [appliedCouponCode, setAppliedCouponCode] = useState<string | null>(null);
  const [couponMessage, setCouponMessage] = useState<string | null>(null);
  const [couponError, setCouponError] = useState<string | null>(null);
  const [isApplyingCoupon, setIsApplyingCoupon] = useState(false);

  const serviceQuery = useQuery({
    queryKey: ["service", serviceSlug],
    queryFn: () => apiFetch<ServiceDetail>(`${API_V1}/services/${serviceSlug}`),
    enabled: !!serviceSlug,
  });

  const addressesQuery = useQuery({
    queryKey: ["addresses"],
    queryFn: () => apiFetch<CustomerAddress[]>(`${API_V1}/addresses`, { authenticated: true }),
  });

  // Default to the customer's default address once addresses load.
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
  const requestReady =
    !!service && !!city && !!locality && !!selectedAddressId && !!selectedSlotWindowId;

  const request: BookingSummaryRequestBody | null = requestReady
    ? {
        serviceId: service.id,
        cityId: city.id,
        addressId: selectedAddressId,
        localityId: locality.id,
        slotWindowId: selectedSlotWindowId,
        slotDate: selectedDate,
        quantity,
        addOns: Array.from(selectedAddOnIds, (addOnId) => ({ addOnId, quantity: 1 })),
        // Keeping the applied coupon on the shared request means every
        // recompute (quantity/add-on/slot changes) and the eventual booking
        // creation call all honour it consistently, instead of it being a
        // display-only side channel that gets dropped at checkout time.
        couponCode: appliedCouponCode,
      }
    : null;

  const summaryQuery = useQuery({
    queryKey: ["booking-summary", request],
    queryFn: () =>
      apiFetch<BookingSummary>(`${API_V1}/bookings/summary`, {
        method: "POST",
        authenticated: true,
        body: JSON.stringify(request),
      }),
    enabled: request !== null,
  });

  const handleApplyCoupon = async () => {
    const code = couponInput.trim();
    if (!request || !code) return;
    setIsApplyingCoupon(true);
    setCouponError(null);
    setCouponMessage(null);

    try {
      const result = await apiFetch<BookingSummary>(`${API_V1}/coupons/apply`, {
        method: "POST",
        authenticated: true,
        body: JSON.stringify({ ...request, couponCode: code }),
      });
      setAppliedCouponCode(code);
      const discount = result.coupon?.discountAmount ?? 0;
      setCouponMessage(`${result.coupon?.code ?? code} applied - you saved ₹${discount.toFixed(2)}.`);
    } catch (err) {
      setCouponError(describeError(err));
    } finally {
      setIsApplyingCoupon(false);
    }
  };

  const handleRemoveCoupon = () => {
    setAppliedCouponCode(null);
    setCouponInput("");
    setCouponMessage(null);
    setCouponError(null);
  };

  const handleProceed = async () => {
    if (!request || !service) return;
    setSubmitError(null);
    setIsSubmitting(true);

    try {
      // Re-check the selected slot right before booking (SRS 11.8.3, task
      // 63c) so a slot that went stale while the customer was reviewing the
      // summary fails with a clear, specific message instead of a generic
      // booking-creation error.
      const revalidation = await apiFetch<SlotRevalidation>(
        `${API_V1}/slots/revalidate?serviceId=${request.serviceId}&localityId=${request.localityId}&slotWindowId=${request.slotWindowId}&date=${request.slotDate}`,
      );

      if (!revalidation.isValid) {
        setSelectedSlotWindowId(null);
        setSubmitError(revalidation.reason ?? "This slot is no longer available. Please choose another.");
        setIsSubmitting(false);
        return;
      }

      const booking = await apiFetch<{ id: string }>(`${API_V1}/bookings`, {
        method: "POST",
        authenticated: true,
        body: JSON.stringify(request),
      });

      router.push(`/booking/payment/${booking.id}?serviceSlug=${service.slug}`);
    } catch (err) {
      setSubmitError(describeError(err));
      setIsSubmitting(false);
    }
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

  return (
    <main className="mx-auto grid w-full max-w-4xl gap-6 px-6 py-10 md:grid-cols-[1fr_360px]">
      <div className="flex flex-col gap-6">
        <PageHeading title="Review your booking" subtitle={service.name} />

        {/* Cart: service + add-ons (task 62a). */}
        <Card title="Service">
          <div className="flex items-center justify-between gap-3 text-sm">
            <span className="font-medium">{service.name}</span>
            <span>₹{service.price}</span>
          </div>

          <div className="mt-3 flex items-center justify-between">
            <span className="text-sm font-medium">Quantity</span>
            <div className="flex items-center gap-3">
              <button
                type="button"
                aria-label="Decrease quantity"
                onClick={() => setQuantity((q) => Math.max(1, q - 1))}
                className="h-8 w-8 rounded-lg border border-black/15 text-sm dark:border-white/20"
              >
                −
              </button>
              <span className="w-6 text-center text-sm">{quantity}</span>
              <button
                type="button"
                aria-label="Increase quantity"
                onClick={() => setQuantity((q) => q + 1)}
                className="h-8 w-8 rounded-lg border border-black/15 text-sm dark:border-white/20"
              >
                +
              </button>
            </div>
          </div>

          {service.addOns.length > 0 ? (
            <fieldset className="mt-4 flex flex-col gap-2 border-t border-black/10 pt-4 dark:border-white/15">
              <legend className="mb-1 text-sm font-medium">Add-ons</legend>
              {service.addOns.map((addOn) => (
                <label key={addOn.id} className="flex items-center justify-between gap-3 text-sm">
                  <span className="flex items-center gap-2">
                    <input
                      type="checkbox"
                      checked={selectedAddOnIds.has(addOn.id)}
                      onChange={() => toggleAddOn(addOn.id)}
                      className="h-4 w-4 rounded border-black/25 dark:border-white/30"
                    />
                    {addOn.name}
                  </span>
                  <span className="text-neutral-500">+₹{addOn.price}</span>
                </label>
              ))}
            </fieldset>
          ) : null}
        </Card>

        {/* Address selection (task 62b). */}
        <Card title="Service address" description="Where should we send your professional?">
          {addressesQuery.isPending ? (
            <p className="text-sm text-neutral-500">Loading addresses…</p>
          ) : addressesQuery.isError ? (
            <Alert>{describeError(addressesQuery.error)}</Alert>
          ) : addressesQuery.data.length === 0 ? (
            <div className="flex flex-col items-start gap-2">
              <p className="text-sm text-neutral-600 dark:text-neutral-400">
                You don&apos;t have any saved addresses yet.
              </p>
              <Link href="/addresses/new">
                <Button type="button" variant="secondary">
                  Add an address
                </Button>
              </Link>
            </div>
          ) : (
            <div className="flex flex-col gap-2">
              {addressesQuery.data.map((address) => (
                <label
                  key={address.id}
                  className={`flex cursor-pointer items-start gap-3 rounded-lg border p-3 text-sm ${
                    address.id === selectedAddressId
                      ? "border-black dark:border-white"
                      : "border-black/15 dark:border-white/20"
                  }`}
                >
                  <input
                    type="radio"
                    name="address"
                    className="mt-1"
                    checked={address.id === selectedAddressId}
                    onChange={() => setSelectedAddressId(address.id)}
                  />
                  <span>
                    <span className="font-medium">{address.label}</span>
                    <br />
                    <span className="text-neutral-600 dark:text-neutral-400">
                      {address.line1}
                      {address.line2 ? `, ${address.line2}` : ""}, {address.city} {address.pincode}
                    </span>
                  </span>
                </label>
              ))}
              <Link href="/addresses/new" className="text-sm font-medium hover:underline">
                + Add a new address
              </Link>
            </div>
          )}
        </Card>

        {/* Slot selection (task 62c, 63a-c). */}
        <Card title="Slot" description="Pick a date and time window.">
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

        {/* Coupon (task 62d, 77; SRS 11.10.3). */}
        <Card title="Coupon">
          <div className="flex flex-col gap-3">
            {appliedCouponCode ? (
              <div className="flex items-center justify-between gap-3 rounded-lg border border-black/15 px-3 py-2 text-sm dark:border-white/20">
                <span className="font-medium">{appliedCouponCode}</span>
                <Button type="button" variant="secondary" onClick={handleRemoveCoupon}>
                  Remove
                </Button>
              </div>
            ) : (
              <div className="flex gap-2">
                <input
                  type="text"
                  value={couponInput}
                  onChange={(e) => setCouponInput(e.target.value)}
                  placeholder="Enter coupon code"
                  className="flex-1 rounded-lg border border-black/15 bg-transparent px-3 py-2 text-sm outline-none focus:border-black focus:ring-1 focus:ring-black dark:border-white/20 dark:focus:border-white dark:focus:ring-white"
                />
                <Button
                  type="button"
                  variant="secondary"
                  disabled={!request || !couponInput.trim() || isApplyingCoupon}
                  onClick={handleApplyCoupon}
                >
                  {isApplyingCoupon ? "Applying…" : "Apply"}
                </Button>
              </div>
            )}
            {couponMessage ? <Alert tone="success">{couponMessage}</Alert> : null}
            {couponError ? <Alert>{couponError}</Alert> : null}
          </div>
        </Card>
      </div>

      <aside className="flex flex-col gap-4 md:sticky md:top-6 md:self-start">
        {summaryQuery.isPending && request !== null ? (
          <Card title="Price summary">
            <p className="text-sm text-neutral-500">Calculating…</p>
          </Card>
        ) : summaryQuery.isError ? (
          <Card title="Price summary">
            <Alert>{describeError(summaryQuery.error)}</Alert>
          </Card>
        ) : summaryQuery.data ? (
          <BookingSummaryCard summary={summaryQuery.data} />
        ) : (
          <Card title="Price summary">
            <p className="text-sm text-neutral-500">
              Choose your address and slot to see the price breakdown.
            </p>
          </Card>
        )}

        {submitError ? <Alert>{submitError}</Alert> : null}

        <Button
          type="button"
          className="w-full"
          disabled={!summaryQuery.data || isSubmitting}
          onClick={handleProceed}
        >
          {isSubmitting ? "Placing booking…" : "Proceed to book"}
        </Button>
      </aside>
    </main>
  );
}

/**
 * Price breakdown (task 62e) + policy summary (task 62f, SRS 11.7.2), with
 * the coupon discount and recomputed final payable folded in (task 77) - the
 * discount line and the total both visibly change whenever a coupon is
 * applied or removed, satisfying SRS 11.10.3's "recompute" requirement.
 */
function BookingSummaryCard({ summary }: { summary: BookingSummary }) {
  return (
    <Card title="Price summary">
      <div className="flex flex-col gap-3">
        <PriceRows breakdown={summary.price} />

        {summary.coupon ? (
          <dl className="flex flex-col gap-1.5 border-t border-black/10 pt-3 text-sm dark:border-white/15">
            <div className="flex items-center justify-between text-green-700 dark:text-green-400">
              <dt>Coupon ({summary.coupon.code})</dt>
              <dd>-₹{summary.coupon.discountAmount.toFixed(2)}</dd>
            </div>
            <div className="flex items-center justify-between font-semibold">
              <dt>Final payable</dt>
              <dd>₹{summary.finalPayable.toFixed(2)}</dd>
            </div>
          </dl>
        ) : null}

        {summary.cancellationPolicy || summary.reschedulePolicy ? (
          <div className="border-t border-black/10 pt-3 text-xs text-neutral-600 dark:border-white/15 dark:text-neutral-400">
            <p className="mb-1 font-medium uppercase tracking-wide text-neutral-500">Policy</p>
            {summary.cancellationPolicy ? <p>{summary.cancellationPolicy}</p> : null}
            {summary.reschedulePolicy ? <p>{summary.reschedulePolicy}</p> : null}
          </div>
        ) : null}
      </div>
    </Card>
  );
}

// Mirrors PriceCalculator.tsx's PriceSummary intentionally rather than
// importing it - that component's internal layout is verified/working and
// not worth coupling to this page's needs.
function PriceRows({ breakdown }: { breakdown: PriceBreakdown }) {
  return (
    <dl className="flex flex-col gap-1.5 text-sm">
      <Row label={`Base price × ${breakdown.quantity}`} value={breakdown.baseTotal} />
      {breakdown.addOnLineItems.map((item) => (
        <Row key={item.addOnId} label={`${item.name} × ${item.quantity}`} value={item.lineTotal} />
      ))}
      {breakdown.visitCharge > 0 ? <Row label="Visit charge" value={breakdown.visitCharge} /> : null}
      <Row label={`Tax (${breakdown.taxPercentage}%)`} value={breakdown.taxAmount} />
      {breakdown.platformFee > 0 ? <Row label="Platform fee" value={breakdown.platformFee} /> : null}
      <div className="mt-1 flex items-center justify-between border-t border-black/10 pt-2 font-semibold dark:border-white/15">
        <dt>Total payable</dt>
        <dd>₹{breakdown.totalPayable.toFixed(2)}</dd>
      </div>
    </dl>
  );
}

function Row({ label, value }: { label: string; value: number }) {
  return (
    <div className="flex items-center justify-between text-neutral-600 dark:text-neutral-400">
      <dt>{label}</dt>
      <dd>₹{value.toFixed(2)}</dd>
    </div>
  );
}
