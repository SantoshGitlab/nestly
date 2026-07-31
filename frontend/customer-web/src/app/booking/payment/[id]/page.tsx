"use client";

import { useQuery } from "@tanstack/react-query";
import { useParams, useRouter, useSearchParams } from "next/navigation";
import { Suspense, useEffect, useState } from "react";
import { RequireAuth } from "@/components/RequireAuth";
import { Alert, Button, Card, PageHeading } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import { BookingStatus } from "@/lib/types";
import type { BookingDetail, PaymentOrderResponse, PaymentTransactionResponse } from "@/lib/types";

/**
 * Sandbox payment page (tasks 76a-c): initiates a gateway order for a
 * PaymentPending/PaymentFailed booking, lets the customer simulate completing
 * payment (there is no real gateway - see SandboxPaymentGateway on the
 * backend), and handles the outcome (success redirects to the confirmation
 * page, failure surfaces a retry affordance).
 *
 * Wrapped in Suspense for useSearchParams (see booking/summary/page.tsx for
 * the same pattern).
 */
export default function BookingPaymentPage() {
  return (
    <Suspense fallback={<main className="mx-auto w-full max-w-2xl px-6 py-12" />}>
      <RequireAuth>
        <BookingPaymentScreen />
      </RequireAuth>
    </Suspense>
  );
}

function BookingPaymentScreen() {
  const router = useRouter();
  const { id } = useParams<{ id: string }>();
  const serviceSlug = useSearchParams().get("serviceSlug");
  const successHref = `/booking/success/${id}${serviceSlug ? `?serviceSlug=${serviceSlug}` : ""}`;

  // Bumping this re-runs the order-creation query below with a fresh key,
  // which is exactly what a retry needs: POST /payments/orders again starts
  // a new attempt on a PaymentFailed booking (the backend owns that
  // semantics - see PaymentsController).
  const [attempt, setAttempt] = useState(0);

  const [isPaying, setIsPaying] = useState(false);
  const [payError, setPayError] = useState<string | null>(null);
  const [paymentFailed, setPaymentFailed] = useState(false);
  const [failureReason, setFailureReason] = useState<string | null>(null);

  const bookingQuery = useQuery({
    queryKey: ["booking", id],
    queryFn: () => apiFetch<BookingDetail>(`${API_V1}/bookings/${id}`, { authenticated: true }),
  });

  const booking = bookingQuery.data;
  const isConfirmed = booking?.status === BookingStatus.Confirmed;

  // Already paid (e.g. the customer navigated back here after paying) - skip
  // straight to the confirmation page rather than trying to pay again.
  useEffect(() => {
    if (isConfirmed) router.replace(successHref);
  }, [isConfirmed, router, successHref]);

  const orderQuery = useQuery({
    queryKey: ["payment-order", id, attempt],
    queryFn: () =>
      apiFetch<PaymentOrderResponse>(`${API_V1}/payments/orders`, {
        method: "POST",
        authenticated: true,
        body: JSON.stringify({ bookingId: id, idempotencyKey: null }),
      }),
    enabled: !!booking && !isConfirmed,
  });

  const handlePay = async () => {
    if (!orderQuery.data) return;
    setIsPaying(true);
    setPayError(null);
    setPaymentFailed(false);
    setFailureReason(null);

    try {
      await apiFetch(`${API_V1}/payments/orders/simulate`, {
        method: "POST",
        authenticated: true,
        body: JSON.stringify({ gatewayOrderId: orderQuery.data.gatewayOrderId }),
      });

      // The simulate call only confirms the request itself went through -
      // the actual outcome (Confirmed vs PaymentFailed) has to be read back
      // off the booking.
      const refreshed = await bookingQuery.refetch();
      const status = refreshed.data?.status;

      if (status === BookingStatus.Confirmed) {
        router.push(successHref);
        return;
      }

      if (status === BookingStatus.PaymentFailed) {
        setPaymentFailed(true);
        try {
          const transaction = await apiFetch<PaymentTransactionResponse>(
            `${API_V1}/payments/bookings/${id}`,
            { authenticated: true },
          );
          const lastAttempt = transaction.attempts[transaction.attempts.length - 1];
          setFailureReason(lastAttempt?.failureReason ?? null);
        } catch {
          // Failure reason is a nice-to-have; the retry flow works without it.
        }
      } else {
        setPayError("We couldn't confirm the payment outcome. Please check your bookings page shortly.");
      }
    } catch (err) {
      setPayError(describeError(err));
    } finally {
      setIsPaying(false);
    }
  };

  const handleRetry = () => {
    setPaymentFailed(false);
    setPayError(null);
    setFailureReason(null);
    setAttempt((a) => a + 1);
  };

  if (bookingQuery.isPending) {
    return <main className="mx-auto w-full max-w-2xl px-6 py-12 text-sm text-neutral-500">Loading…</main>;
  }

  if (bookingQuery.isError || !booking) {
    return (
      <main className="mx-auto w-full max-w-2xl px-6 py-12">
        <Alert>{describeError(bookingQuery.error)}</Alert>
      </main>
    );
  }

  if (isConfirmed) {
    return (
      <main className="mx-auto w-full max-w-2xl px-6 py-12 text-sm text-neutral-500">Redirecting…</main>
    );
  }

  return (
    <main className="mx-auto w-full max-w-2xl px-6 py-12">
      <PageHeading title="Sandbox Payment" subtitle={booking.service.name} />

      <Card title="Booking">
        <dl className="flex flex-col gap-2 text-sm">
          <div className="flex items-center justify-between">
            <dt className="text-neutral-600 dark:text-neutral-400">Booking ID</dt>
            <dd className="font-medium">{booking.id}</dd>
          </div>
          <div className="flex items-center justify-between">
            <dt className="text-neutral-600 dark:text-neutral-400">Status</dt>
            <dd className="font-medium">{booking.statusLabel}</dd>
          </div>
          <div className="mt-1 flex items-center justify-between border-t border-black/10 pt-2 font-semibold dark:border-white/15">
            <dt>Amount payable</dt>
            <dd>₹{booking.price.totalPayable.toFixed(2)}</dd>
          </div>
        </dl>
      </Card>

      <div className="mt-6">
        {orderQuery.isPending ? (
          <Card title="Sandbox Payment">
            <p className="text-sm text-neutral-500">Preparing payment…</p>
          </Card>
        ) : orderQuery.isError ? (
          <Card title="Sandbox Payment">
            <div className="flex flex-col gap-3">
              <Alert>{describeError(orderQuery.error)}</Alert>
              <Button type="button" variant="secondary" onClick={handleRetry}>
                Try again
              </Button>
            </div>
          </Card>
        ) : paymentFailed ? (
          <Card title="Payment failed">
            <div className="flex flex-col gap-3">
              <Alert>
                {failureReason ?? "Your payment could not be completed. No amount was deducted."}
              </Alert>
              <Button type="button" onClick={handleRetry}>
                Retry payment
              </Button>
            </div>
          </Card>
        ) : orderQuery.data ? (
          <Card
            title="Sandbox Payment"
            description="This is a sandbox simulation of the payment gateway - no real payment is processed."
          >
            <div className="flex flex-col gap-4">
              <div className="flex items-center justify-between text-sm">
                <span className="text-neutral-600 dark:text-neutral-400">Amount</span>
                <span className="text-lg font-semibold">
                  ₹{orderQuery.data.amount.toFixed(2)} {orderQuery.data.currency}
                </span>
              </div>

              {payError ? <Alert>{payError}</Alert> : null}

              <Button type="button" className="w-full" disabled={isPaying} onClick={handlePay}>
                {isPaying ? "Processing…" : `Pay ₹${orderQuery.data.amount.toFixed(2)} (Sandbox)`}
              </Button>
            </div>
          </Card>
        ) : null}
      </div>
    </main>
  );
}
