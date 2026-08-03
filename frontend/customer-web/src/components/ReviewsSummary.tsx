"use client";

import { useQuery } from "@tanstack/react-query";
import { Alert, Button, Skeleton, cx } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import type { ServiceReviewSummary } from "@/lib/types";

/** Rating summary + recent reviews (SRS 11.6.1 "Reviews and rating summary", task 52f). */
export function ReviewsSummary({ slug }: { slug: string }) {
  const query = useQuery({
    queryKey: ["service-reviews-summary", slug],
    queryFn: () => apiFetch<ServiceReviewSummary>(`${API_V1}/services/${slug}/reviews-summary`),
  });

  return (
    <section aria-labelledby="reviews-heading">
      <h2 id="reviews-heading" className="mb-4 text-lg font-semibold tracking-tight text-fg">
        Reviews &amp; ratings
      </h2>

      {query.isPending ? (
        <div className="flex flex-col gap-4">
          <Skeleton className="h-14 w-48" />
          <Skeleton className="h-24 w-full max-w-xs" />
        </div>
      ) : query.isError ? (
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
      ) : query.data.totalCount === 0 ? (
        <p className="rounded-xl bg-surface-2 px-4 py-3 text-sm text-fg-muted">
          No reviews yet — be the first once your booking is complete.
        </p>
      ) : (
        <div className="flex flex-col gap-5">
          <div className="flex items-center gap-4">
            <span className="nums text-display-sm font-semibold text-fg">
              {query.data.averageRating.toFixed(1)}
            </span>
            <div>
              <Stars rating={query.data.averageRating} />
              <p className="mt-1 text-sm text-fg-muted">
                {query.data.totalCount} review{query.data.totalCount === 1 ? "" : "s"}
              </p>
            </div>
          </div>

          <RatingBreakdown
            breakdown={query.data.ratingBreakdown}
            totalCount={query.data.totalCount}
          />

          <ul className="flex flex-col gap-3">
            {query.data.recentReviews.map((review) => (
              <li key={review.id} className="rounded-xl border border-line bg-surface p-4">
                <Stars rating={review.rating} />
                {review.reviewText ? (
                  <p className="mt-2 text-sm leading-relaxed text-fg-muted">{review.reviewText}</p>
                ) : null}
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  );
}

/**
 * Star row for a rating.
 *
 * The previous version printed a literal "★★★★★" next to the average, so a
 * 2.1-rated service displayed five full stars. This fills to the actual value
 * (half-star resolution) and carries the number as its accessible name, since
 * the glyphs themselves say nothing useful to a screen reader.
 */
function Stars({ rating }: { rating: number }) {
  const rounded = Math.round(rating * 2) / 2;

  return (
    <span
      className="flex items-center gap-0.5"
      role="img"
      aria-label={`${rating.toFixed(1)} out of 5 stars`}
    >
      {[1, 2, 3, 4, 5].map((position) => {
        const fill = Math.max(0, Math.min(1, rounded - position + 1));
        return (
          <span key={position} className="relative inline-block h-4 w-4">
            <Star className="absolute inset-0 text-line-strong" />
            {fill > 0 ? (
              <span
                className="absolute inset-0 overflow-hidden"
                style={{ width: `${fill * 100}%` }}
                aria-hidden
              >
                <Star className="h-4 w-4 text-accent-500" />
              </span>
            ) : null}
          </span>
        );
      })}
    </span>
  );
}

function Star({ className = "" }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" fill="currentColor" className={cx("h-4 w-4", className)} aria-hidden>
      <path d="m12 2.5 2.9 5.9 6.6.9-4.8 4.6 1.2 6.5-5.9-3.1-5.9 3.1 1.2-6.5L2.5 9.3l6.6-.9L12 2.5Z" />
    </svg>
  );
}

function RatingBreakdown({
  breakdown,
  totalCount,
}: {
  breakdown: Record<number, number>;
  totalCount: number;
}) {
  return (
    <div className="flex max-w-xs flex-col gap-1.5">
      {[5, 4, 3, 2, 1].map((star) => {
        const count = breakdown[star] ?? 0;
        const percentage = totalCount === 0 ? 0 : Math.round((count / totalCount) * 100);
        return (
          <div key={star} className="flex items-center gap-2 text-xs text-fg-muted">
            <span className="nums w-3 text-right">{star}</span>
            <Star className="h-3 w-3 text-fg-subtle" />
            <div className="h-1.5 flex-1 overflow-hidden rounded-full bg-surface-3">
              <div
                className="h-full rounded-full bg-accent-500 transition-[width] duration-slow ease-out"
                style={{ width: `${percentage}%` }}
              />
            </div>
            <span className="nums w-6 text-right">{count}</span>
          </div>
        );
      })}
    </div>
  );
}
