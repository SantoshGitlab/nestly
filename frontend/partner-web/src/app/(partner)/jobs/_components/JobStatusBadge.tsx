"use client";

import { Badge } from "@/components/ui";
import { JobStatus, jobStatusLabel } from "@/lib/jobs-types";
import type { BadgeTone } from "@/components/ui";

/**
 * Status colour carries the one thing a partner scans a job list for: what
 * still needs an answer from them. Lives in its own module so the list and
 * the detail screen cannot drift apart on what "Assigned" looks like (and
 * because a `page.tsx` may only export a default in the app router).
 */
function toneFor(status: JobStatus): BadgeTone {
  switch (status) {
    case JobStatus.Assigned:
      return "warning";
    case JobStatus.Accepted:
    case JobStatus.InProgress:
      return "info";
    case JobStatus.Completed:
      return "success";
    case JobStatus.Rejected:
    case JobStatus.Withdrawn:
      return "danger";
    default:
      return "neutral";
  }
}

export function JobStatusBadge({ status }: { status: JobStatus }) {
  return <Badge tone={toneFor(status)}>{jobStatusLabel(status)}</Badge>;
}
