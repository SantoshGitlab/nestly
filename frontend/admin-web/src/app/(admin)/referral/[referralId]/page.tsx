"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useParams } from "next/navigation";
import { useState } from "react";
import {
  Breadcrumbs,
  ConfirmDialog,
  DescriptionList,
  FormActions,
  formatCurrency,
  formatDateTime,
} from "@/components/data-table";
import { DetailError, DetailSkeleton } from "@/components/screen-states";
import { Alert, Badge, Button, Card, Field, PageHeading } from "@/components/ui";
import { describeError } from "@/lib/api";
import { canWriteModule } from "@/lib/permissions";
import { useAdminClaims } from "@/lib/use-admin-claims";
import { ReferralStatusBadge } from "../_components/ReferralStatusBadge";
import { getReferral, reviewReferralFraud } from "../_lib/referral-api";
import { REFERRAL_REWARD_TYPE_LABELS } from "../_lib/referral-types";
import type { ReferralAdminDetail } from "../_lib/referral-types";

type FraudAction = "flag" | "approve" | "reject";

/**
 * One referral's detail plus its fraud review decision (task 170).
 *
 * This was an inline panel appended below the list, which meant the
 * irreversible "confirm this is fraud" decision was a single unconfirmed
 * click, the note field was lost the moment the panel closed, and the referral
 * had no addressable URL. It is a route now, matching every other admin detail
 * screen.
 */
export default function ReferralDetailPage() {
  const { referralId } = useParams<{ referralId: string }>();
  const queryClient = useQueryClient();
  const claims = useAdminClaims();
  const canWrite = canWriteModule(claims, "referral");

  const [note, setNote] = useState("");
  const [confirmFraud, setConfirmFraud] = useState(false);

  const referralQuery = useQuery({
    queryKey: ["referrals", "detail", referralId],
    queryFn: () => getReferral(referralId),
  });

  const reviewMutation = useMutation({
    mutationFn: (action: FraudAction) => reviewReferralFraud(referralId, action, { note: note.trim() || null }),
    onSuccess: () => {
      setConfirmFraud(false);
      // The note has been recorded server-side; leaving it in the box invites
      // it being attached a second time to an unrelated decision.
      setNote("");
      queryClient.invalidateQueries({ queryKey: ["referrals", "detail", referralId] });
      // The list's flag column and the fraud queue's membership both change.
      queryClient.invalidateQueries({ queryKey: ["referrals", "search"] });
    },
  });

  const breadcrumbs = [
    { label: "Referrals", href: "/referral" },
    { label: referralQuery.data ? `${referralQuery.data.referrerName} → ${referralQuery.data.refereeName}` : "Referral" },
  ];

  if (referralQuery.isPending) {
    return <DetailSkeleton cards={2} className="flex w-full max-w-7xl flex-col gap-6" />;
  }

  if (referralQuery.error || !referralQuery.data) {
    return (
      <DetailError
        title="Referral"
        breadcrumbs={breadcrumbs}
        error={referralQuery.error}
        message={referralQuery.error ? undefined : "This referral no longer exists."}
        onRetry={() => referralQuery.refetch()}
        className="w-full max-w-7xl"
      />
    );
  }

  const referral = referralQuery.data;
  const isPending = reviewMutation.isPending;
  const pendingAction = isPending ? reviewMutation.variables : undefined;

  return (
    <div className="flex w-full max-w-7xl flex-col gap-6">
      <PageHeading
        title={`${referral.referrerName} → ${referral.refereeName}`}
        subtitle={`Referral code ${referral.referralCodeUsed}`}
        breadcrumbs={<Breadcrumbs items={breadcrumbs} />}
        actions={
          <div className="flex items-center gap-2">
            <ReferralStatusBadge status={referral.status} />
            {referral.isFraudFlagged ? <Badge tone="danger">Flagged</Badge> : null}
          </div>
        }
      />

      <Card title="Participants">
        <DescriptionList items={participantItems(referral)} />
      </Card>

      <Card title="Rewards & timeline">
        <DescriptionList items={rewardItems(referral)} columns={3} />
      </Card>

      {referral.fraudReviewNote ? (
        <Card title="Fraud review note">
          <p className="whitespace-pre-wrap text-sm text-fg">{referral.fraudReviewNote}</p>
          {referral.fraudReviewedAtUtc ? (
            <p className="mt-2 text-xs text-fg-subtle">
              Reviewed {formatDateTime(referral.fraudReviewedAtUtc)}
            </p>
          ) : null}
        </Card>
      ) : null}

      {canWrite ? (
        <Card
          title="Fraud review"
          description={
            referral.isFraudFlagged
              ? "Confirm this is a real abuse pattern, or dismiss the flag as a false positive."
              : "Flag this referral so it appears in the fraud review queue."
          }
        >
          <div className="flex flex-col gap-4">
            {/* While the confirmation is open it owns the error, so the same
                message does not appear twice on screen. */}
            {reviewMutation.isError && !confirmFraud ? (
              <Alert>{describeError(reviewMutation.error)}</Alert>
            ) : null}

            <Field
              label="Review note"
              hint="Optional. Recorded against the referral and shown to whoever reviews it next."
              placeholder="What led to this decision?"
              value={note}
              onChange={(event) => setNote(event.target.value)}
              disabled={isPending}
            />

            <FormActions align="start">
              {referral.isFraudFlagged ? (
                <>
                  <Button
                    type="button"
                    variant="danger"
                    disabled={isPending}
                    onClick={() => {
                      // Otherwise a previous failed "dismiss" would greet the
                      // admin as this dialog's error.
                      reviewMutation.reset();
                      setConfirmFraud(true);
                    }}
                  >
                    Confirm fraud
                  </Button>
                  <Button
                    type="button"
                    variant="secondary"
                    disabled={isPending}
                    loading={pendingAction === "reject"}
                    onClick={() => reviewMutation.mutate("reject")}
                  >
                    Dismiss flag (false positive)
                  </Button>
                </>
              ) : (
                <Button
                  type="button"
                  variant="secondary"
                  disabled={isPending}
                  loading={pendingAction === "flag"}
                  onClick={() => reviewMutation.mutate("flag")}
                >
                  Flag for review
                </Button>
              )}
            </FormActions>
          </div>
        </Card>
      ) : null}

      <ConfirmDialog
        open={confirmFraud}
        title="Confirm this referral as fraud?"
        description="The flag clears and the decision is recorded against both customers. Reversing a reward that has already been paid out is a separate, deliberate action."
        confirmLabel="Confirm fraud"
        cancelLabel="Keep reviewing"
        loading={pendingAction === "approve"}
        error={reviewMutation.error ? describeError(reviewMutation.error) : null}
        onCancel={() => setConfirmFraud(false)}
        onConfirm={() => reviewMutation.mutate("approve")}
      >
        <p className="text-sm text-fg-muted">
          <span className="font-medium text-fg">{referral.referrerName}</span> referred{" "}
          <span className="font-medium text-fg">{referral.refereeName}</span> with code{" "}
          <span className="nums font-medium text-fg">{referral.referralCodeUsed}</span>.
        </p>
      </ConfirmDialog>
    </div>
  );
}

function participantItems(referral: ReferralAdminDetail) {
  return [
    {
      label: "Referrer",
      value: (
        <>
          {referral.referrerName}
          <span className="nums block text-fg-muted">{referral.referrerMobile}</span>
        </>
      ),
    },
    {
      label: "Referee",
      value: (
        <>
          {referral.refereeName}
          <span className="nums block text-fg-muted">{referral.refereeMobile}</span>
        </>
      ),
    },
    { label: "Code used", value: <span className="nums">{referral.referralCodeUsed}</span> },
    {
      label: "Qualifying booking",
      value: referral.qualifyingBookingId ? (
        <span className="nums break-all">{referral.qualifyingBookingId}</span>
      ) : (
        <span className="text-fg-subtle">Not yet qualified</span>
      ),
    },
  ];
}

function rewardItems(referral: ReferralAdminDetail) {
  return [
    {
      label: "Referrer reward",
      value: (
        <span className="nums">
          {formatCurrency(referral.referrerRewardValue)}{" "}
          <span className="text-fg-muted">({REFERRAL_REWARD_TYPE_LABELS[referral.referrerRewardType]})</span>
        </span>
      ),
    },
    {
      label: "Referee reward",
      value: (
        <span className="nums">
          {formatCurrency(referral.refereeRewardValue)}{" "}
          <span className="text-fg-muted">({REFERRAL_REWARD_TYPE_LABELS[referral.refereeRewardType]})</span>
        </span>
      ),
    },
    {
      label: "Min qualifying order",
      value: <span className="nums">{formatCurrency(referral.minQualifyingOrderAmount)}</span>,
    },
    { label: "Registered", value: <span className="nums">{formatDateTime(referral.registeredAtUtc)}</span> },
    { label: "Qualified", value: <span className="nums">{formatDateTime(referral.qualifiedAtUtc)}</span> },
    { label: "Rewarded", value: <span className="nums">{formatDateTime(referral.rewardedAtUtc)}</span> },
    { label: "Expires", value: <span className="nums">{formatDateTime(referral.expiresAtUtc)}</span> },
  ];
}
