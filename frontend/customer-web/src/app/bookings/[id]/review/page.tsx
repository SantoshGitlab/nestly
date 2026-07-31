"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useState } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { RequireAuth } from "@/components/RequireAuth";
import { Alert, Button, Card, PageHeading } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import { ReviewStatus } from "@/lib/types";
import type { ReviewEligibilityResponse, ReviewResponse } from "@/lib/types";

/**
 * Review submission: rating + review form for completed bookings (SRS 25.1,
 * task 91).
 */
export default function ReviewBookingPage() {
  return (
    <RequireAuth>
      <ReviewBookingScreen />
    </RequireAuth>
  );
}

const reviewSchema = z.object({
  reviewText: z.string().max(2000, "Review text must be 2000 characters or fewer.").optional(),
  issueTags: z.string().max(500, "Issue tags must be 500 characters or fewer.").optional(),
});

type ReviewFormValues = z.infer<typeof reviewSchema>;

function reviewStatusLabel(status: ReviewStatus): string {
  switch (status) {
    case ReviewStatus.Visible:
      return "Visible";
    case ReviewStatus.Hidden:
      return "Hidden";
    default:
      return "Unknown";
  }
}

function ReviewBookingScreen() {
  const { id } = useParams<{ id: string }>();
  const queryClient = useQueryClient();

  const eligibilityQuery = useQuery({
    queryKey: ["review-eligibility", id],
    queryFn: () =>
      apiFetch<ReviewEligibilityResponse>(`${API_V1}/bookings/${id}/review/eligibility`, {
        authenticated: true,
      }),
  });

  // undefined body on 204 means no review has been submitted yet.
  const reviewQuery = useQuery({
    queryKey: ["review", id],
    queryFn: () =>
      apiFetch<ReviewResponse | undefined>(`${API_V1}/bookings/${id}/review`, {
        authenticated: true,
      }),
  });

  if (eligibilityQuery.isPending || reviewQuery.isPending) {
    return <main className="mx-auto w-full max-w-2xl px-6 py-12 text-sm text-neutral-500">Loading…</main>;
  }

  if (eligibilityQuery.isError || !eligibilityQuery.data) {
    return (
      <main className="mx-auto w-full max-w-2xl px-6 py-12">
        <Alert>{describeError(eligibilityQuery.error)}</Alert>
      </main>
    );
  }

  if (reviewQuery.isError) {
    return (
      <main className="mx-auto w-full max-w-2xl px-6 py-12">
        <Alert>{describeError(reviewQuery.error)}</Alert>
      </main>
    );
  }

  const existingReview = reviewQuery.data;

  if (existingReview) {
    return (
      <main className="mx-auto w-full max-w-2xl px-6 py-12">
        <PageHeading title="Your review" />
        <Card title="Submitted review">
          <dl className="flex flex-col gap-3 text-sm">
            <div className="flex items-center justify-between">
              <dt className="text-neutral-600 dark:text-neutral-400">Rating</dt>
              <dd className="font-medium">
                <StarDisplay rating={existingReview.rating} />
              </dd>
            </div>
            {existingReview.reviewText ? (
              <div>
                <dt className="text-neutral-600 dark:text-neutral-400">Review</dt>
                <dd className="mt-1">{existingReview.reviewText}</dd>
              </div>
            ) : null}
            {existingReview.issueTags ? (
              <div>
                <dt className="text-neutral-600 dark:text-neutral-400">Issue tags</dt>
                <dd className="mt-1">{existingReview.issueTags}</dd>
              </div>
            ) : null}
            <div className="flex items-center justify-between">
              <dt className="text-neutral-600 dark:text-neutral-400">Status</dt>
              <dd className="font-medium">{reviewStatusLabel(existingReview.status)}</dd>
            </div>
          </dl>
          <div className="mt-5">
            <Link href={`/bookings/${id}`} className="text-sm font-medium hover:underline">
              Back to booking
            </Link>
          </div>
        </Card>
      </main>
    );
  }

  const eligibility = eligibilityQuery.data;

  if (!eligibility.isEligible) {
    return (
      <main className="mx-auto w-full max-w-2xl px-6 py-12">
        <PageHeading title="Leave a review" />
        <Alert>{eligibility.ineligibilityReason ?? "This booking isn't eligible for a review."}</Alert>
        <div className="mt-5">
          <Link href={`/bookings/${id}`} className="text-sm font-medium hover:underline">
            Back to booking
          </Link>
        </div>
      </main>
    );
  }

  return <ReviewForm bookingId={id} onSubmitted={() => queryClient.invalidateQueries({ queryKey: ["review", id] })} />;
}

function ReviewForm({ bookingId, onSubmitted }: { bookingId: string; onSubmitted: () => void }) {
  const [rating, setRatingState] = useState<number>(0);
  const [ratingError, setRatingError] = useState<string | null>(null);

  const form = useForm<ReviewFormValues>({
    resolver: zodResolver(reviewSchema),
    defaultValues: { reviewText: "", issueTags: "" },
  });
  const { errors } = form.formState;

  const submitMutation = useMutation({
    mutationFn: (values: ReviewFormValues) =>
      apiFetch<ReviewResponse>(`${API_V1}/bookings/${bookingId}/review`, {
        method: "POST",
        authenticated: true,
        body: JSON.stringify({
          rating,
          reviewText: values.reviewText?.trim() ? values.reviewText.trim() : null,
          issueTags: values.issueTags?.trim() ? values.issueTags.trim() : null,
        }),
      }),
    onSuccess: onSubmitted,
  });

  const handleSubmit = form.handleSubmit((values) => {
    if (rating < 1 || rating > 5) {
      setRatingError("Please select a rating from 1 to 5.");
      return;
    }
    setRatingError(null);
    submitMutation.mutate(values);
  });

  return (
    <main className="mx-auto w-full max-w-2xl px-6 py-12">
      <PageHeading title="Leave a review" subtitle="Tell us how the service went." />

      <Card title="Rate your experience">
        <form onSubmit={handleSubmit} className="flex flex-col gap-4" noValidate>
          {submitMutation.isError ? <Alert>{describeError(submitMutation.error)}</Alert> : null}

          <div className="flex flex-col gap-1.5">
            <span className="text-sm font-medium">Rating</span>
            <StarInput value={rating} onChange={setRatingState} />
            {ratingError ? <p className="text-xs text-red-600 dark:text-red-400">{ratingError}</p> : null}
          </div>

          <div className="flex flex-col gap-1.5">
            <label htmlFor="review-text" className="text-sm font-medium">
              Review (optional)
            </label>
            <textarea
              id="review-text"
              rows={4}
              maxLength={2000}
              aria-invalid={errors.reviewText ? true : undefined}
              aria-describedby={errors.reviewText ? "review-text-error" : undefined}
              className="rounded-lg border border-black/15 bg-transparent px-3 py-2 text-sm outline-none focus:border-black focus:ring-1 focus:ring-black dark:border-white/20 dark:focus:border-white dark:focus:ring-white"
              {...form.register("reviewText")}
            />
            {errors.reviewText ? (
              <p id="review-text-error" className="text-xs text-red-600 dark:text-red-400">
                {errors.reviewText.message}
              </p>
            ) : null}
          </div>

          <div className="flex flex-col gap-1.5">
            <label htmlFor="review-tags" className="text-sm font-medium">
              Issue tags (optional)
            </label>
            <input
              id="review-tags"
              type="text"
              placeholder="e.g. late arrival, pricing"
              maxLength={500}
              aria-invalid={errors.issueTags ? true : undefined}
              aria-describedby={errors.issueTags ? "review-tags-error" : undefined}
              className="rounded-lg border border-black/15 bg-transparent px-3 py-2 text-sm outline-none focus:border-black focus:ring-1 focus:ring-black dark:border-white/20 dark:focus:border-white dark:focus:ring-white"
              {...form.register("issueTags")}
            />
            {errors.issueTags ? (
              <p id="review-tags-error" className="text-xs text-red-600 dark:text-red-400">
                {errors.issueTags.message}
              </p>
            ) : null}
          </div>

          <Button type="submit" disabled={submitMutation.isPending}>
            {submitMutation.isPending ? "Submitting…" : "Submit review"}
          </Button>
        </form>
      </Card>
    </main>
  );
}

/** Clickable 1-5 star rating input - a one-off control, not a shared primitive. */
function StarInput({ value, onChange }: { value: number; onChange: (rating: number) => void }) {
  return (
    <div role="radiogroup" aria-label="Rating" className="flex gap-1">
      {[1, 2, 3, 4, 5].map((n) => (
        <button
          key={n}
          type="button"
          role="radio"
          aria-checked={value === n}
          aria-label={`${n} star${n === 1 ? "" : "s"}`}
          onClick={() => onChange(n)}
          className="text-2xl leading-none"
        >
          <span aria-hidden="true" className={n <= value ? "text-black dark:text-white" : "text-neutral-300 dark:text-neutral-700"}>
            ★
          </span>
        </button>
      ))}
    </div>
  );
}

function StarDisplay({ rating }: { rating: number }) {
  return (
    <span aria-label={`${rating} out of 5 stars`}>
      {[1, 2, 3, 4, 5].map((n) => (
        <span key={n} aria-hidden="true" className={n <= rating ? "text-black dark:text-white" : "text-neutral-300 dark:text-neutral-700"}>
          ★
        </span>
      ))}
    </span>
  );
}
