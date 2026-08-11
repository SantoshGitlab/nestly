"use client";

import { Badge } from "@/components/ui";
import { RecurrenceFrequency, recurrenceFrequencyLabel } from "@/lib/jobs-types";

/**
 * Marks a job that came from a customer's recurring plan (task 300) and says
 * how often it repeats.
 *
 * A provider's job list is otherwise a flat stream of one-off visits, and the
 * two are worth very different amounts of care: a standing weekly customer is
 * a relationship to protect, not a single fare. The cadence rides inside the
 * badge rather than sitting in a separate field because "recurring" without
 * "how often" leaves the provider no better able to plan than before.
 *
 * Its own module, like `JobStatusBadge`, so the list and any later screen
 * cannot drift on what a recurring job looks like.
 */
export function RecurringJobBadge({ frequency }: { frequency: RecurrenceFrequency | null }) {
  return (
    // `brand`, not one of the tones JobStatusBadge already uses
    // (warning/info/success/danger/neutral) - the two badges sit side by side
    // and a shared colour would read as a shared meaning.
    <Badge tone="brand" className="whitespace-nowrap">
      <svg
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
        className="h-3.5 w-3.5 shrink-0"
        aria-hidden
      >
        <path d="M17 2l4 4-4 4" />
        <path d="M3 11v-1a4 4 0 0 1 4-4h14" />
        <path d="M7 22l-4-4 4-4" />
        <path d="M21 13v1a4 4 0 0 1-4 4H3" />
      </svg>
      {/* The plan id can be present without a cadence only if the plan row
          vanished behind a Restrict foreign key that forbids it - degrade to
          the bare word rather than rendering "undefined". */}
      {frequency === null ? "Recurring" : `Recurring · ${recurrenceFrequencyLabel(frequency)}`}
    </Badge>
  );
}
