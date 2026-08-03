"use client";

import { Badge } from "@/components/ui";
import { PayoutStatus, payoutStatusLabel } from "@/lib/earnings-types";
import type { BadgeTone } from "@/components/ui";

/**
 * Payout status colour, in its own module so the payouts list and the payout
 * detail screen cannot drift apart on what "Failed" looks like (and because a
 * `page.tsx` may only export a default in the app router).
 *
 * Mirrors jobs/_components/JobStatusBadge.tsx: `Failed` is the only state that
 * needs the partner to do anything, so it is the only one in the danger tone.
 */
function toneFor(status: PayoutStatus): BadgeTone {
  switch (status) {
    case PayoutStatus.Paid:
      return "success";
    case PayoutStatus.Processing:
      return "info";
    case PayoutStatus.Failed:
      return "danger";
    case PayoutStatus.Pending:
    default:
      return "neutral";
  }
}

export function PayoutStatusBadge({ status }: { status: PayoutStatus }) {
  return <Badge tone={toneFor(status)}>{payoutStatusLabel(status)}</Badge>;
}
