"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { Alert, Button, Card, Field, PageHeading } from "@/components/ui";
import { API_V1, apiFetch, apiFetchBlob, describeError } from "@/lib/api";
import { getSessionClaims, subscribeToAuthChanges } from "@/lib/auth";
import {
  DEFAULT_REVIEW_MODERATION_FILTERS,
  FLAGGED_FILTER_OPTIONS,
  REVIEW_STATUS_FILTER_OPTIONS,
  buildReviewModerationQuery,
  reviewStatusLabel,
} from "@/lib/reviews";
import type { ReviewModerationFilters } from "@/lib/reviews";
import { ReviewStatus } from "@/lib/types";
import type {
  AdminSessionClaims,
  ModerateReviewRequestBody,
  ReviewModerationItem,
  ReviewModerationSearchResponse,
} from "@/lib/types";

const PAGE_SIZE = 20;
const selectClassName =
  "rounded-lg border border-black/15 bg-transparent px-3 py-2 text-sm outline-none focus:border-black focus:ring-1 focus:ring-black dark:border-white/20 dark:focus:border-white dark:focus:ring-white";
const labelClassName = "flex flex-col gap-1.5 text-sm font-medium";

function useAdminClaims(): AdminSessionClaims | null {
  const [claims, setClaims] = useState<AdminSessionClaims | null>(null);
  useEffect(() => {
    const sync = () => setClaims(getSessionClaims());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);
  return claims;
}

function StatusBadge({ status }: { status: ReviewStatus }) {
  const tone =
    status === ReviewStatus.Hidden
      ? "border-red-300 bg-red-50 text-red-900 dark:border-red-900 dark:bg-red-950 dark:text-red-100"
      : "border-green-300 bg-green-50 text-green-900 dark:border-green-900 dark:bg-green-950 dark:text-green-100";

  return (
    <span className={`inline-flex rounded-full border px-2 py-0.5 text-xs font-medium ${tone}`}>
      {reviewStatusLabel(status)}
    </span>
  );
}

function FlaggedBadge() {
  return (
    <span className="inline-flex rounded-full border border-amber-300 bg-amber-50 px-2 py-0.5 text-xs font-medium text-amber-900 dark:border-amber-800 dark:bg-amber-950 dark:text-amber-100">
      Flagged
    </span>
  );
}

function StarRating({ rating }: { rating: number }) {
  return (
    <span aria-label={`${rating} out of 5 stars`} className="tabular-nums">
      {"★".repeat(rating)}
      <span className="text-neutral-300 dark:text-neutral-700">{"★".repeat(5 - rating)}</span>
    </span>
  );
}

/**
 * Admin review moderation screen (SRS 12.15, task 123): filterable review
 * list (status, flagged, rating range, date, service/category) with
 * hide/unhide, flag/unflag and CSV export actions - see ReviewsController
 * (task 122) for the API this drives. Moderating actions are only shown to
 * admins holding "reviews.write"; the API enforces this server-side
 * regardless, same rationale as the customer detail screen's `canWrite` gate.
 */
export default function ReviewModerationPage() {
  const claims = useAdminClaims();
  const canWrite = claims?.permissions.includes("reviews.write") ?? false;
  const queryClient = useQueryClient();

  const [draft, setDraft] = useState<ReviewModerationFilters>(DEFAULT_REVIEW_MODERATION_FILTERS);
  const [applied, setApplied] = useState<ReviewModerationFilters>(DEFAULT_REVIEW_MODERATION_FILTERS);
  const [page, setPage] = useState(1);
  const [noteDrafts, setNoteDrafts] = useState<Record<string, string>>({});
  const [actionError, setActionError] = useState<string | null>(null);
  const [exportError, setExportError] = useState<string | null>(null);
  const [isExporting, setIsExporting] = useState(false);

  const query = useQuery({
    queryKey: ["admin-reviews", applied, page],
    queryFn: () =>
      apiFetch<ReviewModerationSearchResponse>(
        `${API_V1}/reviews?${buildReviewModerationQuery(applied, { page, pageSize: PAGE_SIZE })}`,
        { authenticated: true },
      ),
  });

  const invalidateList = () => queryClient.invalidateQueries({ queryKey: ["admin-reviews"] });

  const noteFor = (reviewId: string) => noteDrafts[reviewId]?.trim() || null;
  const setNoteFor = (reviewId: string, value: string) => setNoteDrafts((current) => ({ ...current, [reviewId]: value }));

  function useModerationMutation(action: "hide" | "unhide" | "flag" | "unflag") {
    return useMutation({
      mutationFn: ({ reviewId, note }: { reviewId: string; note: string | null }) =>
        apiFetch<ReviewModerationItem>(`${API_V1}/reviews/${reviewId}/${action}`, {
          method: "POST",
          authenticated: true,
          body: JSON.stringify({ note } satisfies ModerateReviewRequestBody),
        }),
      onSuccess: (_data, variables) => {
        setActionError(null);
        setNoteFor(variables.reviewId, "");
        invalidateList();
      },
      onError: (err) => setActionError(describeError(err)),
    });
  }

  const hideMutation = useModerationMutation("hide");
  const unhideMutation = useModerationMutation("unhide");
  const flagMutation = useModerationMutation("flag");
  const unflagMutation = useModerationMutation("unflag");

  const onApply = (event: FormEvent) => {
    event.preventDefault();
    setPage(1);
    setApplied(draft);
  };

  const onReset = () => {
    setDraft(DEFAULT_REVIEW_MODERATION_FILTERS);
    setApplied(DEFAULT_REVIEW_MODERATION_FILTERS);
    setPage(1);
  };

  const onExport = async () => {
    setIsExporting(true);
    setExportError(null);
    try {
      const blob = await apiFetchBlob(
        `${API_V1}/reviews/export?${buildReviewModerationQuery(applied, { page: 1, pageSize: PAGE_SIZE })}`,
        { authenticated: true },
      );
      const url = URL.createObjectURL(blob);
      const link = document.createElement("a");
      link.href = url;
      link.download = `reviews-export-${new Date().toISOString().slice(0, 10)}.csv`;
      document.body.appendChild(link);
      link.click();
      link.remove();
      URL.revokeObjectURL(url);
    } catch (err) {
      setExportError(describeError(err));
    } finally {
      setIsExporting(false);
    }
  };

  const totalPages = query.data ? Math.max(1, Math.ceil(query.data.totalCount / PAGE_SIZE)) : 1;

  return (
    <div className="mx-auto w-full max-w-5xl">
      <PageHeading
        title="Review Moderation"
        subtitle="View, hide/unhide, flag and export customer reviews (SRS 12.15). The original rating and text are always retained, even once hidden or flagged."
      />

      <Card title="Filters" description="All fields are optional; leave a field blank to not filter on it.">
        <form onSubmit={onApply} className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <label className={labelClassName}>
            Status
            <select
              className={selectClassName}
              value={draft.status}
              onChange={(e) => setDraft((f) => ({ ...f, status: e.target.value as ReviewModerationFilters["status"] }))}
            >
              {REVIEW_STATUS_FILTER_OPTIONS.map((option) => (
                <option key={option.label} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>

          <label className={labelClassName}>
            Flagged
            <select
              className={selectClassName}
              value={draft.flagged}
              onChange={(e) => setDraft((f) => ({ ...f, flagged: e.target.value as ReviewModerationFilters["flagged"] }))}
            >
              {FLAGGED_FILTER_OPTIONS.map((option) => (
                <option key={option.label} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>

          <div className="grid grid-cols-2 gap-2">
            <Field
              label="Min rating"
              type="number"
              min={1}
              max={5}
              value={draft.minRating}
              onChange={(e) => setDraft((f) => ({ ...f, minRating: e.target.value }))}
            />
            <Field
              label="Max rating"
              type="number"
              min={1}
              max={5}
              value={draft.maxRating}
              onChange={(e) => setDraft((f) => ({ ...f, maxRating: e.target.value }))}
            />
          </div>

          <div className="grid grid-cols-2 gap-2">
            <Field
              label="Posted from"
              type="date"
              value={draft.fromDate}
              onChange={(e) => setDraft((f) => ({ ...f, fromDate: e.target.value }))}
            />
            <Field
              label="Posted to"
              type="date"
              value={draft.toDate}
              onChange={(e) => setDraft((f) => ({ ...f, toDate: e.target.value }))}
            />
          </div>

          <Field
            label="Service ID"
            placeholder="Service GUID"
            value={draft.serviceId}
            onChange={(e) => setDraft((f) => ({ ...f, serviceId: e.target.value }))}
          />

          <Field
            label="Category ID"
            placeholder="Category GUID"
            value={draft.categoryId}
            onChange={(e) => setDraft((f) => ({ ...f, categoryId: e.target.value }))}
          />

          <div className="flex items-end gap-2 sm:col-span-2 lg:col-span-3">
            <Button type="submit">Apply filters</Button>
            <Button type="button" variant="secondary" onClick={onReset}>
              Reset
            </Button>
            <Button type="button" variant="secondary" onClick={onExport} disabled={isExporting}>
              {isExporting ? "Exporting…" : "Export CSV"}
            </Button>
          </div>
        </form>
        {exportError ? (
          <div className="mt-3">
            <Alert>{exportError}</Alert>
          </div>
        ) : null}
      </Card>

      {actionError ? (
        <div className="mt-6">
          <Alert>{actionError}</Alert>
        </div>
      ) : null}

      <div className="mt-6 flex flex-col gap-4">
        {query.isPending ? (
          <p className="text-sm text-neutral-500">Loading reviews…</p>
        ) : query.isError ? (
          <Alert>{describeError(query.error)}</Alert>
        ) : query.data.items.length === 0 ? (
          <Card title="No reviews match these filters">
            <p className="text-sm text-neutral-600 dark:text-neutral-400">Try broadening your search criteria.</p>
          </Card>
        ) : (
          <>
            {query.data.items.map((review) => (
              <div
                key={review.id}
                className="w-full rounded-xl border border-black/10 bg-white p-5 shadow-sm dark:border-white/15 dark:bg-neutral-900"
              >
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <div className="flex flex-wrap items-center gap-2">
                    <StarRating rating={review.rating} />
                    <StatusBadge status={review.status} />
                    {review.isFlagged ? <FlaggedBadge /> : null}
                  </div>
                  <span className="text-xs text-neutral-500 dark:text-neutral-400">
                    {new Date(review.createdAtUtc).toLocaleString()}
                  </span>
                </div>

                <p className="mt-2 text-sm text-neutral-600 dark:text-neutral-400">
                  {review.customerName} · {review.serviceName} ({review.categoryName})
                </p>

                {review.reviewText ? <p className="mt-3 text-sm">{review.reviewText}</p> : null}
                {review.issueTags ? (
                  <p className="mt-2 text-xs text-neutral-500 dark:text-neutral-400">Tags: {review.issueTags}</p>
                ) : null}

                {review.moderatorNote ? (
                  <p className="mt-3 rounded-lg bg-black/5 px-3 py-2 text-xs text-neutral-700 dark:bg-white/5 dark:text-neutral-300">
                    Moderator note: {review.moderatorNote}
                    {review.moderatedAtUtc ? ` (${new Date(review.moderatedAtUtc).toLocaleString()})` : null}
                  </p>
                ) : null}

                {canWrite ? (
                  <div className="mt-4 flex flex-col gap-2 border-t border-black/10 pt-4 dark:border-white/15 sm:flex-row sm:items-end">
                    <div className="flex-1">
                      <Field
                        label="Moderator note (optional)"
                        value={noteDrafts[review.id] ?? ""}
                        onChange={(e) => setNoteFor(review.id, e.target.value)}
                        placeholder="Reason for this action"
                      />
                    </div>
                    <div className="flex flex-wrap gap-2">
                      {review.status === ReviewStatus.Hidden ? (
                        <Button
                          variant="secondary"
                          disabled={unhideMutation.isPending}
                          onClick={() => unhideMutation.mutate({ reviewId: review.id, note: noteFor(review.id) })}
                        >
                          {unhideMutation.isPending ? "Unhiding…" : "Unhide"}
                        </Button>
                      ) : (
                        <Button
                          variant="danger"
                          disabled={hideMutation.isPending}
                          onClick={() => hideMutation.mutate({ reviewId: review.id, note: noteFor(review.id) })}
                        >
                          {hideMutation.isPending ? "Hiding…" : "Hide"}
                        </Button>
                      )}

                      {review.isFlagged ? (
                        <Button
                          variant="secondary"
                          disabled={unflagMutation.isPending}
                          onClick={() => unflagMutation.mutate({ reviewId: review.id, note: noteFor(review.id) })}
                        >
                          {unflagMutation.isPending ? "Unflagging…" : "Unflag"}
                        </Button>
                      ) : (
                        <Button
                          variant="secondary"
                          disabled={flagMutation.isPending}
                          onClick={() => flagMutation.mutate({ reviewId: review.id, note: noteFor(review.id) })}
                        >
                          {flagMutation.isPending ? "Flagging…" : "Flag"}
                        </Button>
                      )}
                    </div>
                  </div>
                ) : null}
              </div>
            ))}

            <div className="flex items-center justify-between text-sm">
              <span className="text-neutral-600 dark:text-neutral-400">
                Page {page} of {totalPages} · {query.data.totalCount} review{query.data.totalCount === 1 ? "" : "s"}
              </span>
              <div className="flex gap-2">
                <Button variant="secondary" disabled={page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>
                  Previous
                </Button>
                <Button
                  variant="secondary"
                  disabled={page >= totalPages}
                  onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                >
                  Next
                </Button>
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
