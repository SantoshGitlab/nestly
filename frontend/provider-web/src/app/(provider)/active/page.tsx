"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { ErrorState, NotYetAvailable } from "@/components/states";
import { Button, Card, EmptyState, PageHeading, Skeleton } from "@/components/ui";
import { isNotImplemented } from "@/lib/api";
import { formatInr, formatIsoDate, formatTime } from "@/lib/format";
import { listJobs } from "@/lib/jobs-api";
import { listInProgressJobs } from "@/lib/jobs-active";
import { JobStatus } from "@/lib/jobs-types";
import { JobStatusBadge } from "../jobs/_components/JobStatusBadge";
import { RecurringJobBadge } from "../jobs/_components/RecurringJobBadge";
import type { JobListItem } from "@/lib/jobs-types";

/** Same purpose as `/today`'s own map, scoped to the three statuses this screen shows. */
const NEXT_ACTION_LABEL: Partial<Record<JobStatus, string>> = {
  [JobStatus.EnRoute]: "Mark arrived",
  [JobStatus.Arrived]: "Start job",
  [JobStatus.InProgress]: "Finish job",
};

/**
 * Active Job screen (docs/OPEN-FIXES-FEATURES.csv "Provider Web, In Progress
 * job own page"): a dedicated view for the job(s) a provider is literally,
 * physically working right now - EnRoute/Arrived/InProgress
 * (`listInProgressJobs`, `lib/jobs-active.ts`). Distinct from `/today`'s
 * broader "single most urgent thing" (a fresh offer outranks an in-progress
 * job there, so a provider mid-visit can lose sight of the job they're
 * actually doing) and from `/offers`' pending-decision queue - this screen
 * only ever shows work already underway.
 *
 * Same thin pattern as `/today`: never mutates a job itself, only routes
 * into the existing `/jobs/[id]` detail page for every action - see that
 * screen's own doc comment on why the completion-verification gating and
 * status-transition logic is not duplicated here.
 */
export default function ActiveJobPage() {
  const query = useQuery({
    // Same key/query `/jobs` and `/today` use with no filters applied -
    // shares their cache rather than firing another identical fetch.
    queryKey: ["provider-jobs", "", ""],
    queryFn: () => listJobs({}),
  });

  return (
    <div>
      <PageHeading title="Active job" subtitle="What you're working on right now." />

      {query.isPending ? (
        <ActiveJobSkeleton />
      ) : query.isError && isNotImplemented(query.error) ? (
        <NotYetAvailable
          title="Active job isn't available yet"
          description="Job assignment is still being built on the platform side. Once your account can receive bookings, they will appear here."
        />
      ) : query.isError ? (
        <ErrorState
          title="Couldn't load your jobs"
          error={query.error}
          onRetry={() => query.refetch()}
          isRetrying={query.isRefetching}
        />
      ) : (
        <ActiveJobContent jobs={query.data} />
      )}
    </div>
  );
}

function ActiveJobContent({ jobs }: { jobs: JobListItem[] }) {
  const activeJobs = listInProgressJobs(jobs);

  if (activeJobs.length === 0) {
    return (
      <EmptyState
        title="No active job right now"
        description="Once you start a job, it stays here - with its next action - until you mark it complete."
        action={
          <Link href="/today">
            <Button variant="secondary">Go to Today</Button>
          </Link>
        }
      />
    );
  }

  return (
    <div className="flex flex-col gap-4">
      {activeJobs.map((job) => (
        <ActiveJobCard key={job.bookingId} job={job} />
      ))}
    </div>
  );
}

function ActiveJobCard({ job }: { job: JobListItem }) {
  const isRecurring = job.recurringBookingPlanId !== null;
  const nextActionLabel = NEXT_ACTION_LABEL[job.status] ?? "View job";

  return (
    <Card>
      <div className="flex flex-col gap-5">
        <div className="flex items-start justify-between gap-3">
          <div className="flex min-w-0 flex-wrap items-center gap-1.5">
            <JobStatusBadge status={job.status} />
            {isRecurring ? <RecurringJobBadge frequency={job.recurringFrequency} /> : null}
          </div>
          {/* Net payout, not the customer's gross booking total - see
              JobListItem.netAmountToProvider's doc comment. */}
          <span className="nums shrink-0 text-base font-semibold text-fg">
            {formatInr(job.netAmountToProvider)}
          </span>
        </div>

        <div className="min-w-0">
          <p className="truncate text-xl font-semibold text-fg">{job.customerNameSnapshot}</p>
          <p className="mt-1 nums text-sm text-fg-muted">
            {formatIsoDate(job.slotDate)} · {formatTime(job.slotStartTimeSnapshot)}–
            {formatTime(job.slotEndTimeSnapshot)}
          </p>
          <p className="nums mt-0.5 text-xs text-fg-subtle">{job.bookingReference}</p>
        </div>

        <p className="text-sm leading-relaxed text-fg-subtle">
          {job.addressLine1Snapshot}, {job.addressCitySnapshot} {job.addressPincodeSnapshot}
        </p>

        <Link href={`/jobs/${job.bookingId}`}>
          <Button
            type="button"
            size="lg"
            fullWidth
            icon={
              <svg
                viewBox="0 0 24 24"
                fill="none"
                stroke="currentColor"
                strokeWidth="2.5"
                strokeLinecap="round"
                strokeLinejoin="round"
                className="h-5 w-5"
                aria-hidden
              >
                <path d="M5 12h14M13 6l6 6-6 6" />
              </svg>
            }
          >
            {nextActionLabel}
          </Button>
        </Link>
      </div>
    </Card>
  );
}

/** Mirrors the real card's shape so nothing jumps when data lands. */
function ActiveJobSkeleton() {
  return (
    <div className="flex flex-col gap-3 rounded-2xl bg-surface p-6 shadow-sm" aria-hidden>
      <div className="flex items-start justify-between gap-3">
        <Skeleton className="h-5 w-20 rounded-full" />
        <Skeleton className="h-5 w-20" />
      </div>
      <Skeleton className="h-6 w-48" />
      <Skeleton className="h-4 w-56" />
      <Skeleton className="h-4 w-full" />
      <Skeleton className="mt-2 h-11 w-full rounded-lg" />
    </div>
  );
}
