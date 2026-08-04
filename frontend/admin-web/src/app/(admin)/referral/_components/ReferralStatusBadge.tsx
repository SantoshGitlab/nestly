"use client";

import { Badge } from "@/components/ui";
import type { BadgeTone } from "@/components/ui";
import { REFERRAL_STATUS_LABELS, ReferralStatus } from "../_lib/referral-types";

/**
 * Status → tone mapping for `ReferralStatus`, mirroring
 * `components/status-badges.tsx`'s treatment of the other admin domain enums:
 * colour here is information, so the mapping lives in one place and every tone
 * is a semantic token.
 */
const REFERRAL_STATUS_TONES: Record<ReferralStatus, BadgeTone> = {
  [ReferralStatus.Registered]: "neutral",
  [ReferralStatus.Qualified]: "info",
  [ReferralStatus.Rewarded]: "success",
  [ReferralStatus.Expired]: "warning",
};

export function ReferralStatusBadge({ status }: { status: ReferralStatus }) {
  return (
    <Badge tone={REFERRAL_STATUS_TONES[status] ?? "neutral"}>
      {REFERRAL_STATUS_LABELS[status] ?? "Unknown"}
    </Badge>
  );
}

/** Shown only when a referral is actually flagged — an empty cell otherwise reads as "no". */
export function FraudFlagBadge({ flagged }: { flagged: boolean }) {
  return flagged ? (
    <Badge tone="danger">Flagged</Badge>
  ) : (
    <span className="text-fg-subtle">—</span>
  );
}
