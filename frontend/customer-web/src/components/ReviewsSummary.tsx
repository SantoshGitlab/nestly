"use client";

import { useQuery } from "@tanstack/react-query";
import { Alert } from "@/components/ui";
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
      <h2 id="reviews-heading" className="mb-2 text-sm font-semibold uppercase tracking-wide text-neutral-500">
        Reviews &amp; ratings
      </h2>

      {query.isPending ? (
        <p className="text-sm text-neutral-500">Loading reviews…</p>
      ) : query.isError ? (
        <Alert>{describeError(query.error)}</Alert>
      ) : query.data.totalCount === 0 ? (
        <p className="text-sm text-neutral-500">No reviews yet.</p>
      ) : (
        <div className="flex flex-col gap-4">
          <div className="flex items-center gap-3">
            <span className="text-2xl font-semibold">{query.data.averageRating.toFixed(1)}</span>
            <div className="text-sm text-neutral-600 dark:text-neutral-400">
              <div>★★★★★</div>
              <div>
                {query.data.totalCount} review{query.data.totalCount === 1 ? "" : "s"}
              </div>
            </div>
          </div>

          <RatingBreakdown breakdown={query.data.ratingBreakdown} totalCount={query.data.totalCount} />

          <ul className="flex flex-col gap-3">
            {query.data.recentReviews.map((review) => (
              <li key={review.id} className="rounded-lg border border-black/10 p-3 text-sm dark:border-white/15">
                <span className="font-medium">{"★".repeat(review.rating)}</span>
                {review.reviewText ? (
                  <p className="mt-1 text-neutral-600 dark:text-neutral-400">{review.reviewText}</p>
                ) : null}
              </li>
            ))}
          </ul>
        </div>
      )}
    </section>
  );
}

function RatingBreakdown({ breakdown, totalCount }: { breakdown: Record<number, number>; totalCount: number }) {
  return (
    <div className="flex flex-col gap-1">
      {[5, 4, 3, 2, 1].map((star) => {
        const count = breakdown[star] ?? 0;
        const percentage = totalCount === 0 ? 0 : Math.round((count / totalCount) * 100);
        return (
          <div key={star} className="flex items-center gap-2 text-xs text-neutral-500">
            <span className="w-3">{star}</span>
            <div className="h-1.5 flex-1 rounded-full bg-black/10 dark:bg-white/10">
              <div className="h-full rounded-full bg-black dark:bg-white" style={{ width: `${percentage}%` }} />
            </div>
            <span className="w-6 text-right">{count}</span>
          </div>
        );
      })}
    </div>
  );
}
