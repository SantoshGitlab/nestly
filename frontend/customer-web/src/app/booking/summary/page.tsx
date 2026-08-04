"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useEffect, useRef, useState } from "react";
import type { ReactNode } from "react";
import { CitySelector } from "@/components/CitySelector";
import { LocalitySelector } from "@/components/LocalitySelector";
import {
  BookingProgress,
  PriceBreakdownList,
  PriceBreakdownSkeleton,
  STICKY_BAR_SPACER,
  ScreenSkeleton,
  StickyActionBar,
  inr,
} from "@/components/patterns";
import { RequireAuth } from "@/components/RequireAuth";
import { SlotPicker } from "@/components/SlotPicker";
import {
  Alert,
  Button,
  Card,
  EmptyState,
  PageHeading,
  Skeleton,
  cx,
} from "@/components/ui";
import { useSelectedCity } from "@/hooks/useSelectedCity";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import { todayIsoDate } from "@/lib/date";
import type {
  BookingSummary,
  BookingSummaryRequestBody,
  CustomerAddress,
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
    <Suspense fallback={<ScreenSkeleton cards={4} />}>
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
  const [selectedDate, setSelectedDate] = useState<string>(todayIsoDate);
  const [selectedSlotWindowId, setSelectedSlotWindowId] = useState<string | null>(null);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  /**
   * Double-submit guard. `isSubmitting` disables the button, but on a slow
   * connection a second click can land in the same tick as the first, before
   * React has re-rendered the disabled state - and a duplicate POST /bookings
   * creates a second real booking the customer then has to cancel. A ref is
   * checked and set synchronously, so the second call returns immediately.
   */
  const inFlight = useRef(false);

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
      setCouponMessage(`${result.coupon?.code ?? code} applied — you saved ${inr(discount)}.`);
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
    // Synchronous re-entry guard - see `inFlight` above.
    if (inFlight.current) return;
    inFlight.current = true;

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
        setSubmitError(
          revalidation.reason ?? "This slot is no longer available. Please choose another.",
        );
        return;
      }

      const booking = await apiFetch<{ id: string }>(`${API_V1}/bookings`, {
        method: "POST",
        authenticated: true,
        body: JSON.stringify(request),
      });

      router.push(`/booking/payment/${booking.id}?serviceSlug=${service.slug}`);
      // Deliberately no reset here: the navigation is in flight and leaving
      // the button busy stops a second submit during the route transition.
      return;
    } catch (err) {
      // Everything the customer entered - quantity, add-ons, address, slot,
      // coupon - is component state and is untouched by a failure, so a retry
      // costs one click and no re-entry.
      setSubmitError(describeError(err));
    } finally {
      if (!inFlight.current) return;
      inFlight.current = false;
      setIsSubmitting(false);
    }
  };

  if (!serviceSlug) {
    return (
      <main className="mx-auto w-full max-w-3xl px-4 py-12 sm:px-6">
        <EmptyState
          title="No service selected"
          description="Choose a service first and we'll bring you straight back here to review it."
          action={
            <Link
              href="/categories"
              className="inline-flex h-10 items-center justify-center rounded-lg bg-brand-600 px-4 text-sm font-medium text-fg-on-brand shadow-brand transition duration-fast ease-out hover:bg-brand-700"
            >
              Browse services
            </Link>
          }
        />
      </main>
    );
  }

  if (serviceQuery.isPending) {
    return <ScreenSkeleton cards={4} className="mx-auto w-full max-w-4xl px-4 py-8 sm:px-6 sm:py-10" />;
  }

  if (serviceQuery.isError || !service) {
    return (
      <main className="mx-auto w-full max-w-3xl px-4 py-12 sm:px-6">
        <Alert
          tone="error"
          title="Couldn't load this service"
          action={
            <Button size="sm" variant="secondary" onClick={() => serviceQuery.refetch()}>
              Retry
            </Button>
          }
        >
          {describeError(serviceQuery.error)}
        </Alert>
      </main>
    );
  }

  const summary = summaryQuery.data;
  const payable = summary ? (summary.coupon ? summary.finalPayable : summary.price.totalPayable) : null;

  return (
    <main
      className={cx(
        "mx-auto grid w-full max-w-4xl gap-6 px-4 py-8 sm:px-6 sm:py-10 md:grid-cols-[1fr_22rem]",
        STICKY_BAR_SPACER,
      )}
    >
      <div className="flex min-w-0 flex-col gap-6 md:col-start-1">
        <div>
          <BookingProgress current={0} />
          <PageHeading title="Review your booking" subtitle={service.name} />
        </div>

        {/* Cart: service + add-ons (task 62a). */}
        <Card title="Service">
          <div className="flex items-baseline justify-between gap-4">
            <span className="text-sm font-medium text-fg">{service.name}</span>
            <span className="nums text-sm text-fg-muted">{inr(service.price)}</span>
          </div>

          <div className="mt-4 flex items-center justify-between gap-4">
            <span className="text-sm font-medium text-fg">Quantity</span>
            <div className="flex items-center gap-2">
              <QuantityButton
                label="Decrease quantity"
                onClick={() => setQuantity((q) => Math.max(1, q - 1))}
                disabled={quantity <= 1}
              >
                −
              </QuantityButton>
              <span className="nums w-8 text-center text-sm font-medium text-fg" aria-live="polite">
                {quantity}
              </span>
              <QuantityButton label="Increase quantity" onClick={() => setQuantity((q) => q + 1)}>
                +
              </QuantityButton>
            </div>
          </div>

          {service.addOns.length > 0 ? (
            <fieldset className="mt-5 border-t border-line pt-4">
              <legend className="mb-2.5 text-sm font-medium text-fg">Add-ons</legend>
              <div className="flex flex-col gap-2">
                {service.addOns.map((addOn) => {
                  const checked = selectedAddOnIds.has(addOn.id);
                  return (
                    <label
                      key={addOn.id}
                      className={cx(
                        "flex cursor-pointer items-center justify-between gap-3 rounded-xl border px-3.5 py-2.5 text-sm transition duration-fast ease-out",
                        checked
                          ? "border-brand-600/40 bg-brand-50 dark:bg-brand-500/10"
                          : "border-line bg-surface hover:border-line-strong hover:bg-surface-2",
                      )}
                    >
                      <span className="flex min-w-0 items-center gap-2.5">
                        <input
                          type="checkbox"
                          checked={checked}
                          onChange={() => toggleAddOn(addOn.id)}
                          className="h-4 w-4 shrink-0 cursor-pointer rounded border-line-strong accent-brand-600"
                        />
                        <span className="min-w-0 truncate text-fg">{addOn.name}</span>
                      </span>
                      <span className="nums shrink-0 text-fg-muted">+{inr(addOn.price)}</span>
                    </label>
                  );
                })}
              </div>
            </fieldset>
          ) : null}
        </Card>

        {/* Address selection (task 62b). */}
        <Card title="Service address" description="Where should we send your professional?">
          {addressesQuery.isPending ? (
            <div className="flex flex-col gap-2" aria-hidden>
              <Skeleton className="h-[4.5rem] rounded-xl" />
              <Skeleton className="h-[4.5rem] rounded-xl" />
            </div>
          ) : addressesQuery.isError ? (
            <Alert
              tone="error"
              title="Couldn't load your addresses"
              action={
                <Button size="sm" variant="secondary" onClick={() => addressesQuery.refetch()}>
                  Retry
                </Button>
              }
            >
              {describeError(addressesQuery.error)}
            </Alert>
          ) : addressesQuery.data.length === 0 ? (
            <EmptyState
              title="No saved addresses"
              description="Add the address you'd like this service delivered to."
              action={
                <Link
                  href="/addresses/new"
                  className="inline-flex h-10 items-center justify-center rounded-lg bg-brand-600 px-4 text-sm font-medium text-fg-on-brand shadow-brand transition duration-fast ease-out hover:bg-brand-700"
                >
                  Add an address
                </Link>
              }
            />
          ) : (
            <div className="flex flex-col gap-2">
              {addressesQuery.data.map((address) => {
                const isSelected = address.id === selectedAddressId;
                return (
                  <label
                    key={address.id}
                    className={cx(
                      "flex cursor-pointer items-start gap-3 rounded-xl border p-3.5 text-sm transition duration-fast ease-out",
                      isSelected
                        ? "border-brand-600 bg-brand-50 shadow-xs dark:bg-brand-500/10"
                        : "border-line bg-surface hover:border-line-strong hover:bg-surface-2",
                    )}
                  >
                    <input
                      type="radio"
                      name="address"
                      className="mt-0.5 h-4 w-4 shrink-0 cursor-pointer accent-brand-600"
                      checked={isSelected}
                      onChange={() => setSelectedAddressId(address.id)}
                    />
                    <span className="min-w-0">
                      <span className="flex items-center gap-2 font-medium text-fg">
                        {address.label}
                        {address.isDefault ? (
                          <span className="rounded-full bg-surface-3 px-2 py-0.5 text-xs font-medium text-fg-muted">
                            Default
                          </span>
                        ) : null}
                      </span>
                      <span className="mt-0.5 block leading-relaxed text-fg-muted">
                        {address.line1}
                        {address.line2 ? `, ${address.line2}` : ""}, {address.city}{" "}
                        <span className="nums">{address.pincode}</span>
                      </span>
                    </span>
                  </label>
                );
              })}
              <Link
                href="/addresses/new"
                className="mt-1 inline-flex w-fit items-center gap-1 text-sm font-medium text-brand-600 underline-offset-4 hover:underline dark:text-brand-400"
              >
                + Add a new address
              </Link>
            </div>
          )}
        </Card>

        {/* Slot selection (task 62c, 63a-c). */}
        <Card title="Slot" description="Pick a date and time window.">
          {city === undefined ? (
            <Skeleton className="h-24 rounded-xl" />
          ) : city === null ? (
            <div className="flex flex-col items-start gap-2.5">
              <p className="text-sm text-fg-muted">Select your city first.</p>
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
        <Card title="Coupon" description="Have a code? Apply it before you pay.">
          <div className="flex flex-col gap-3">
            {appliedCouponCode ? (
              <div className="flex items-center justify-between gap-3 rounded-xl border border-success/25 bg-success-soft px-3.5 py-2.5">
                <span className="font-mono text-sm font-semibold uppercase tracking-wide text-success">
                  {appliedCouponCode}
                </span>
                <Button type="button" size="sm" variant="secondary" onClick={handleRemoveCoupon}>
                  Remove
                </Button>
              </div>
            ) : (
              <div className="flex gap-2">
                <input
                  type="text"
                  id="coupon-code"
                  aria-label="Coupon code"
                  value={couponInput}
                  onChange={(e) => setCouponInput(e.target.value)}
                  placeholder="Enter coupon code"
                  className="w-full rounded-lg border border-line bg-surface px-3 py-2 text-sm uppercase text-fg shadow-xs outline-none transition duration-fast ease-out placeholder:normal-case placeholder:text-fg-subtle hover:border-line-strong focus:border-brand-600 focus:ring-2 focus:ring-brand-600/25"
                />
                <Button
                  type="button"
                  variant="secondary"
                  loading={isApplyingCoupon}
                  disabled={!request || !couponInput.trim()}
                  onClick={handleApplyCoupon}
                >
                  Apply
                </Button>
              </div>
            )}
            {!appliedCouponCode && !request ? (
              <p className="text-xs text-fg-subtle">
                Choose an address and a slot first — a coupon is validated against the full booking.
              </p>
            ) : null}
            {couponMessage ? <Alert tone="success">{couponMessage}</Alert> : null}
            {couponError ? <Alert tone="error">{couponError}</Alert> : null}
          </div>
        </Card>
      </div>

      <aside className="flex flex-col gap-4 md:sticky md:top-20 md:col-start-2 md:row-start-1 md:self-start">
        {summaryQuery.isPending && request !== null ? (
          <Card title="Price summary">
            <PriceBreakdownSkeleton />
          </Card>
        ) : summaryQuery.isError ? (
          <Card title="Price summary">
            <Alert
              tone="error"
              title="Couldn't price this booking"
              action={
                <Button size="sm" variant="secondary" onClick={() => summaryQuery.refetch()}>
                  Retry
                </Button>
              }
            >
              {describeError(summaryQuery.error)}
            </Alert>
          </Card>
        ) : summary ? (
          <BookingSummaryCard summary={summary} />
        ) : (
          <Card title="Price summary">
            <p className="text-sm leading-relaxed text-fg-muted">
              Choose your address and slot to see the full price breakdown.
            </p>
          </Card>
        )}

        {submitError ? (
          <Alert
            tone="error"
            title="We couldn't place your booking"
            action={
              <Button size="sm" variant="secondary" onClick={handleProceed} disabled={!request}>
                Try again
              </Button>
            }
          >
            {submitError} Nothing you entered has been lost.
          </Alert>
        ) : null}

        <StickyActionBar>
          {payable !== null ? (
            <div className="flex items-baseline justify-between gap-3 md:hidden">
              <span className="text-xs font-medium uppercase tracking-wide text-fg-muted">
                Total payable
              </span>
              <span className="nums text-lg font-semibold text-fg">{inr(payable)}</span>
            </div>
          ) : null}

          <Button
            type="button"
            size="lg"
            fullWidth
            loading={isSubmitting}
            disabled={!summary}
            onClick={handleProceed}
          >
            Proceed to book
          </Button>

          {/* Announced rather than shown as a blocking overlay: the button's
              own spinner covers the visual case, this covers screen readers. */}
          <p role="status" aria-live="polite" className="sr-only">
            {isSubmitting ? "Placing your booking, please wait." : ""}
          </p>

        </StickyActionBar>

        {/* Entry point into task 187's recurring-plan setup flow - same
            service, no booking placed yet. A styled Link rather than
            <Link><Button/></Link>: a button inside an anchor is invalid HTML
            and gives assistive tech two nested controls for one action. */}
        <Link
          href={`/recurring-bookings/new?serviceSlug=${service.slug}`}
          className="inline-flex h-10 w-full items-center justify-center rounded-lg border border-line bg-surface text-sm font-medium text-fg shadow-xs transition duration-fast ease-out hover:border-line-strong hover:bg-surface-2"
        >
          Set up as recurring instead
        </Link>
      </aside>
    </main>
  );
}

function QuantityButton({
  label,
  onClick,
  disabled,
  children,
}: {
  label: string;
  onClick: () => void;
  disabled?: boolean;
  children: ReactNode;
}) {
  return (
    <button
      type="button"
      aria-label={label}
      onClick={onClick}
      disabled={disabled}
      className="flex h-9 w-9 items-center justify-center rounded-lg border border-line bg-surface text-base text-fg shadow-xs transition duration-fast ease-out hover:border-line-strong hover:bg-surface-2 active:scale-[0.96] disabled:cursor-not-allowed disabled:opacity-45 disabled:active:scale-100"
    >
      {children}
    </button>
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
      <div className="flex flex-col gap-4">
        <PriceBreakdownList
          breakdown={summary.price}
          discount={
            summary.coupon
              ? { code: summary.coupon.code, amount: summary.coupon.discountAmount }
              : null
          }
          total={summary.coupon ? summary.finalPayable : summary.price.totalPayable}
        />

        {summary.coupon ? (
          <p className="rounded-lg bg-success-soft px-3 py-2 text-xs font-medium text-success">
            You saved <span className="nums">{inr(summary.coupon.discountAmount)}</span> with{" "}
            {summary.coupon.code}.
          </p>
        ) : null}

        {summary.cancellationPolicy || summary.reschedulePolicy ? (
          <div className="border-t border-line pt-3">
            <p className="mb-1.5 text-xs font-semibold uppercase tracking-wide text-fg-subtle">
              Policy
            </p>
            <div className="flex flex-col gap-1 text-xs leading-relaxed text-fg-muted">
              {summary.cancellationPolicy ? <p>{summary.cancellationPolicy}</p> : null}
              {summary.reschedulePolicy ? <p>{summary.reschedulePolicy}</p> : null}
            </div>
          </div>
        ) : null}
      </div>
    </Card>
  );
}
