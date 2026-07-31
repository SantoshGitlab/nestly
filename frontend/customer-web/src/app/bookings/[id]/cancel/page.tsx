"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { RequireAuth } from "@/components/RequireAuth";
import { Alert, Button, Card, PageHeading } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import { RefundMethod } from "@/lib/types";
import type {
  CancellationOutcomeResponse,
  CancellationPolicyResponse,
} from "@/lib/types";

/**
 * Cancellation flow: policy display, confirmation, refund outcome (SRS
 * 11.14.3, tasks 89a-c).
 */
export default function CancelBookingPage() {
  return (
    <RequireAuth>
      <CancelBookingScreen />
    </RequireAuth>
  );
}

const cancelSchema = z.object({
  reason: z
    .string()
    .min(1, "Please tell us why you're cancelling.")
    .max(500, "Reason must be 500 characters or fewer."),
});

type CancelFormValues = z.infer<typeof cancelSchema>;

function refundMethodLabel(method: RefundMethod): string {
  return method === RefundMethod.Gateway ? "Original payment method" : "Wallet credit";
}

function CancelBookingScreen() {
  const { id } = useParams<{ id: string }>();
  const router = useRouter();
  const queryClient = useQueryClient();

  const policyQuery = useQuery({
    queryKey: ["cancellation-policy", id],
    queryFn: () =>
      apiFetch<CancellationPolicyResponse>(`${API_V1}/bookings/${id}/cancellation/policy`, {
        authenticated: true,
      }),
  });

  const form = useForm<CancelFormValues>({
    resolver: zodResolver(cancelSchema),
    defaultValues: { reason: "" },
  });
  const { errors } = form.formState;

  const cancelMutation = useMutation({
    mutationFn: (values: CancelFormValues) =>
      apiFetch<CancellationOutcomeResponse>(`${API_V1}/bookings/${id}/cancellation`, {
        method: "POST",
        authenticated: true,
        body: JSON.stringify(values),
      }),
    onSuccess: () => {
      // The booking detail page must reflect the cancellation immediately.
      queryClient.invalidateQueries({ queryKey: ["booking", id] });
    },
  });

  if (policyQuery.isPending) {
    return <main className="mx-auto w-full max-w-2xl px-6 py-12 text-sm text-neutral-500">Loading…</main>;
  }

  if (policyQuery.isError || !policyQuery.data) {
    return (
      <main className="mx-auto w-full max-w-2xl px-6 py-12">
        <Alert>{describeError(policyQuery.error)}</Alert>
      </main>
    );
  }

  const policy = policyQuery.data;

  if (cancelMutation.isSuccess) {
    const outcome = cancelMutation.data;
    return (
      <main className="mx-auto w-full max-w-2xl px-6 py-12">
        <PageHeading title="Booking cancelled" />
        <Card title="Refund outcome">
          <dl className="flex flex-col gap-2 text-sm">
            <div className="flex items-center justify-between">
              <dt className="text-neutral-600 dark:text-neutral-400">Cancellation fee</dt>
              <dd className="font-medium">₹{outcome.cancellationFeeAmount.toFixed(2)}</dd>
            </div>
            <div className="flex items-center justify-between">
              <dt className="text-neutral-600 dark:text-neutral-400">Refund amount</dt>
              <dd className="font-medium">₹{outcome.refundAmount.toFixed(2)}</dd>
            </div>
            {outcome.refundMethod !== null ? (
              <div className="flex items-center justify-between">
                <dt className="text-neutral-600 dark:text-neutral-400">Refund method</dt>
                <dd className="font-medium">{refundMethodLabel(outcome.refundMethod)}</dd>
              </div>
            ) : null}
            <div className="flex items-center justify-between">
              <dt className="text-neutral-600 dark:text-neutral-400">Within free cancellation window</dt>
              <dd className="font-medium">{outcome.withinFreeCancellationWindow ? "Yes" : "No"}</dd>
            </div>
          </dl>
          <div className="mt-5">
            <Link href={`/bookings/${id}`}>
              <Button type="button" className="w-full">
                Back to booking
              </Button>
            </Link>
          </div>
        </Card>
      </main>
    );
  }

  if (!policy.isEligible) {
    return (
      <main className="mx-auto w-full max-w-2xl px-6 py-12">
        <PageHeading title="Cancel booking" />
        <Alert>{policy.ineligibilityReason ?? "This booking cannot be cancelled."}</Alert>
        <div className="mt-5">
          <Link href={`/bookings/${id}`} className="text-sm font-medium hover:underline">
            Back to booking
          </Link>
        </div>
      </main>
    );
  }

  return (
    <main className="mx-auto w-full max-w-2xl px-6 py-12">
      <PageHeading title="Cancel booking" subtitle="Review the cancellation policy before confirming." />

      <div className="flex flex-col gap-6">
        <Card title="Cancellation policy">
          <dl className="flex flex-col gap-2 text-sm">
            <div className="flex items-center justify-between">
              <dt className="text-neutral-600 dark:text-neutral-400">Free cancellation window</dt>
              <dd className="font-medium">{policy.freeCancellationWindowHours} hours before slot</dd>
            </div>
            <div className="flex items-center justify-between">
              <dt className="text-neutral-600 dark:text-neutral-400">Within free window</dt>
              <dd className="font-medium">{policy.withinFreeCancellationWindow ? "Yes" : "No"}</dd>
            </div>
            {!policy.withinFreeCancellationWindow ? (
              <div className="flex items-center justify-between">
                <dt className="text-neutral-600 dark:text-neutral-400">Late cancellation fee</dt>
                <dd className="font-medium">{policy.lateCancellationFeePercentage}%</dd>
              </div>
            ) : null}
            <div className="flex items-center justify-between">
              <dt className="text-neutral-600 dark:text-neutral-400">Cancellation fee</dt>
              <dd className="font-medium">₹{policy.cancellationFeeAmount.toFixed(2)}</dd>
            </div>
            <div className="flex items-center justify-between border-t border-black/10 pt-2 font-semibold dark:border-white/15">
              <dt>Refund amount</dt>
              <dd>₹{policy.refundAmount.toFixed(2)}</dd>
            </div>
            <div className="flex items-center justify-between">
              <dt className="text-neutral-600 dark:text-neutral-400">Refund method</dt>
              <dd className="font-medium">{refundMethodLabel(policy.refundMethod)}</dd>
            </div>
          </dl>
        </Card>

        <Card title="Confirm cancellation">
          <form
            onSubmit={form.handleSubmit((values) => cancelMutation.mutate(values))}
            className="flex flex-col gap-4"
            noValidate
          >
            {cancelMutation.isError ? <Alert>{describeError(cancelMutation.error)}</Alert> : null}

            <div className="flex flex-col gap-1.5">
              <label htmlFor="cancel-reason" className="text-sm font-medium">
                Reason for cancellation
              </label>
              <textarea
                id="cancel-reason"
                rows={4}
                maxLength={500}
                aria-invalid={errors.reason ? true : undefined}
                aria-describedby={errors.reason ? "cancel-reason-error" : undefined}
                className="rounded-lg border border-black/15 bg-transparent px-3 py-2 text-sm outline-none focus:border-black focus:ring-1 focus:ring-black dark:border-white/20 dark:focus:border-white dark:focus:ring-white"
                {...form.register("reason")}
              />
              {errors.reason ? (
                <p id="cancel-reason-error" className="text-xs text-red-600 dark:text-red-400">
                  {errors.reason.message}
                </p>
              ) : null}
            </div>

            <div className="flex gap-3">
              <Button type="submit" variant="danger" disabled={cancelMutation.isPending}>
                {cancelMutation.isPending ? "Cancelling…" : "Confirm cancellation"}
              </Button>
              <Button type="button" variant="secondary" onClick={() => router.back()}>
                Go back
              </Button>
            </div>
          </form>
        </Card>
      </div>
    </main>
  );
}
