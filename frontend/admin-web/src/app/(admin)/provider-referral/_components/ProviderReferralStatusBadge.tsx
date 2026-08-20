"use client";

import { Badge } from "@/components/ui";
import type { BadgeTone } from "@/components/ui";
import { PROVIDER_REFERRAL_STATUS_LABELS, ProviderReferralStatus } from "../_lib/provider-referral-types";

/** Status → tone mapping, mirrors (admin)/referral/_components/ReferralStatusBadge.tsx. */
const PROVIDER_REFERRAL_STATUS_TONES: Record<ProviderReferralStatus, BadgeTone> = {
  [ProviderReferralStatus.Registered]: "neutral",
  [ProviderReferralStatus.Qualified]: "info",
  [ProviderReferralStatus.Rewarded]: "success",
  [ProviderReferralStatus.Expired]: "warning",
};

export function ProviderReferralStatusBadge({ status }: { status: ProviderReferralStatus }) {
  return (
    <Badge tone={PROVIDER_REFERRAL_STATUS_TONES[status] ?? "neutral"}>
      {PROVIDER_REFERRAL_STATUS_LABELS[status] ?? "Unknown"}
    </Badge>
  );
}

/** Shown only when a referral is actually flagged - an empty cell otherwise reads as "no". */
export function ProviderFraudFlagBadge({ flagged }: { flagged: boolean }) {
  return flagged ? <Badge tone="danger">Flagged</Badge> : <span className="text-fg-subtle">—</span>;
}
