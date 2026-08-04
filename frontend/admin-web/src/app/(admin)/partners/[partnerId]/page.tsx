"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useParams } from "next/navigation";
import { useState } from "react";
import {
  Alert,
  Badge,
  Button,
  Card,
  EmptyState,
  Field,
  PageHeading,
  Select,
  SkeletonText,
  StatTile,
} from "@/components/ui";
import {
  Breadcrumbs,
  ConfirmDialog,
  FormActions,
  FormGrid,
  formatCurrency,
  formatDate,
  formatDateTime,
} from "@/components/data-table";
import { DetailError, DetailSkeleton, SectionError } from "@/components/screen-states";
import { PartnerStatusBadge } from "@/components/status-badges";
import { describeError } from "@/lib/api";
import {
  activatePartner,
  approveKycDocument,
  createPayoutBatch,
  getPartnerDetail,
  getPartnerEarnings,
  getPartnerPerformance,
  reactivatePartner,
  recordBackgroundCheck,
  recordEarningAdjustment,
  rejectKycDocument,
  searchPayouts,
  suspendPartner,
  updatePartner,
  updatePayoutStatus,
} from "@/lib/partners-api";
import {
  PartnerBackgroundCheckStatus,
  PartnerEarningEntryType,
  PartnerEarningSourceType,
  PartnerKycDocumentType,
  PartnerKycVerificationStatus,
  PartnerOnboardingStatus,
  PartnerPayoutStatus,
  PartnerStatus,
} from "@/lib/partners-types";
import type { BadgeTone } from "@/components/ui";
import type { PartnerDetail } from "@/lib/partners-types";
import { useAdminClaims } from "@/lib/use-admin-claims";

const STATUS_LABELS: Record<PartnerStatus, string> = {
  [PartnerStatus.PendingVerification]: "Pending verification",
  [PartnerStatus.Active]: "Active",
  [PartnerStatus.Suspended]: "Suspended",
  [PartnerStatus.Deactivated]: "Deactivated",
};

const ONBOARDING_LABELS: Record<PartnerOnboardingStatus, string> = {
  [PartnerOnboardingStatus.Registered]: "Registered",
  [PartnerOnboardingStatus.ProfileCompleted]: "Profile completed",
  [PartnerOnboardingStatus.KycSubmitted]: "KYC submitted",
  [PartnerOnboardingStatus.KycVerified]: "KYC verified",
  [PartnerOnboardingStatus.Completed]: "Onboarding complete",
};

const KYC_DOC_TYPE_LABELS: Record<PartnerKycDocumentType, string> = {
  [PartnerKycDocumentType.IdentityProof]: "Identity proof",
  [PartnerKycDocumentType.AddressProof]: "Address proof",
  [PartnerKycDocumentType.BankAccountProof]: "Bank account proof",
  [PartnerKycDocumentType.ProfessionalCertificate]: "Professional certificate",
  [PartnerKycDocumentType.Other]: "Other",
};

const KYC_STATUS_LABELS: Record<PartnerKycVerificationStatus, string> = {
  [PartnerKycVerificationStatus.Pending]: "Pending review",
  [PartnerKycVerificationStatus.Approved]: "Approved",
  [PartnerKycVerificationStatus.Rejected]: "Rejected",
};

const KYC_STATUS_TONES: Record<PartnerKycVerificationStatus, BadgeTone> = {
  [PartnerKycVerificationStatus.Pending]: "warning",
  [PartnerKycVerificationStatus.Approved]: "success",
  [PartnerKycVerificationStatus.Rejected]: "danger",
};

const BACKGROUND_CHECK_STATUS_LABELS: Record<PartnerBackgroundCheckStatus, string> = {
  [PartnerBackgroundCheckStatus.Pending]: "Pending",
  [PartnerBackgroundCheckStatus.Passed]: "Passed",
  [PartnerBackgroundCheckStatus.Failed]: "Failed",
};

const BACKGROUND_CHECK_TONES: Record<PartnerBackgroundCheckStatus, BadgeTone> = {
  [PartnerBackgroundCheckStatus.Pending]: "warning",
  [PartnerBackgroundCheckStatus.Passed]: "success",
  [PartnerBackgroundCheckStatus.Failed]: "danger",
};

const PAYOUT_STATUS_LABELS: Record<PartnerPayoutStatus, string> = {
  [PartnerPayoutStatus.Pending]: "Pending",
  [PartnerPayoutStatus.Processing]: "Processing",
  [PartnerPayoutStatus.Paid]: "Paid",
  [PartnerPayoutStatus.Failed]: "Failed",
};

const PAYOUT_STATUS_TONES: Record<PartnerPayoutStatus, BadgeTone> = {
  [PartnerPayoutStatus.Pending]: "neutral",
  [PartnerPayoutStatus.Processing]: "info",
  [PartnerPayoutStatus.Paid]: "success",
  [PartnerPayoutStatus.Failed]: "danger",
};

/**
 * Admin partner detail (PARTNER.md; tasks 150a-150c, 160, and the 148
 * financial views): profile edit and suspend/reactivate (150a), KYC document
 * approve/reject and the background-check activation gate (150b, 160), the
 * performance summary (150c), and the earnings ledger / payout batches
 * (148). Mutating actions are only shown to admins holding the relevant
 * "partner.write"/"payout.write" permission - the API enforces this
 * server-side regardless, this purely avoids showing controls that would
 * just 403.
 *
 * Suspension, KYC rejection and marking a payout failed each go through
 * `ConfirmDialog` (task 222) — all three are irreversible from this screen and
 * were previously a single unconfirmed click.
 */
export default function PartnerDetailPage() {
  const params = useParams<{ partnerId: string }>();
  const partnerId = params.partnerId;
  const claims = useAdminClaims();
  const canWritePartner = claims?.permissions.includes("partner.write") ?? false;
  const canWritePayout = claims?.permissions.includes("payout.write") ?? false;
  const queryClient = useQueryClient();

  const detailQuery = useQuery({
    queryKey: ["admin-partner-detail", partnerId],
    queryFn: () => getPartnerDetail(partnerId),
  });
  const performanceQuery = useQuery({
    queryKey: ["admin-partner-performance", partnerId],
    queryFn: () => getPartnerPerformance(partnerId),
  });
  const earningsQuery = useQuery({
    queryKey: ["admin-partner-earnings", partnerId],
    queryFn: () => getPartnerEarnings(partnerId),
  });
  const payoutsQuery = useQuery({
    queryKey: ["admin-partner-payouts", partnerId],
    queryFn: () => searchPayouts(partnerId),
  });

  const [actionError, setActionError] = useState<string | null>(null);
  const [actionNotice, setActionNotice] = useState<string | null>(null);

  const [suspendReason, setSuspendReason] = useState("");
  const [confirmSuspend, setConfirmSuspend] = useState(false);

  const [rejectReasonByDoc, setRejectReasonByDoc] = useState<Record<string, string>>({});
  const [pendingKycRejection, setPendingKycRejection] = useState<{ id: string; label: string } | null>(null);

  const [bgStatus, setBgStatus] = useState(String(PartnerBackgroundCheckStatus.Passed));
  const [bgNotes, setBgNotes] = useState("");

  const [adjustmentType, setAdjustmentType] = useState(String(PartnerEarningEntryType.Credit));
  const [adjustmentAmount, setAdjustmentAmount] = useState("");
  const [adjustmentDescription, setAdjustmentDescription] = useState("");

  const [payoutPeriodStart, setPayoutPeriodStart] = useState("");
  const [payoutPeriodEnd, setPayoutPeriodEnd] = useState("");
  const [payoutReferenceByPayout, setPayoutReferenceByPayout] = useState<Record<string, string>>({});
  const [pendingPayoutFailure, setPendingPayoutFailure] = useState<string | null>(null);

  const invalidateAll = () => {
    queryClient.invalidateQueries({ queryKey: ["admin-partner-detail", partnerId] });
    queryClient.invalidateQueries({ queryKey: ["admin-partner-performance", partnerId] });
    queryClient.invalidateQueries({ queryKey: ["admin-partner-earnings", partnerId] });
    queryClient.invalidateQueries({ queryKey: ["admin-partner-payouts", partnerId] });
  };

  const onError = (err: unknown) => setActionError(describeError(err));
  const onSuccess = (notice: string) => {
    setActionError(null);
    setActionNotice(notice);
    invalidateAll();
  };

  const updateMutation = useMutation({
    mutationFn: (values: { legalName: string; displayName: string; email: string }) =>
      updatePartner(partnerId, {
        legalName: values.legalName,
        displayName: values.displayName,
        email: values.email || undefined,
      }),
    onSuccess: () => onSuccess("Profile updated."),
    onError,
  });

  const suspendMutation = useMutation({
    mutationFn: () => suspendPartner(partnerId, { reason: suspendReason }),
    onSuccess: () => {
      setSuspendReason("");
      setConfirmSuspend(false);
      onSuccess("Partner suspended.");
    },
    onError,
  });

  const reactivateMutation = useMutation({
    mutationFn: () => reactivatePartner(partnerId),
    onSuccess: () => onSuccess("Partner reactivated."),
    onError,
  });

  const activateMutation = useMutation({
    mutationFn: () => activatePartner(partnerId),
    onSuccess: () => onSuccess("Partner activated."),
    onError,
  });

  const approveKycMutation = useMutation({
    mutationFn: (documentId: string) => approveKycDocument(documentId),
    onSuccess: () => onSuccess("KYC document approved."),
    onError,
  });

  const rejectKycMutation = useMutation({
    mutationFn: ({ documentId, reason }: { documentId: string; reason: string }) => rejectKycDocument(documentId, { reason }),
    onSuccess: () => {
      setPendingKycRejection(null);
      onSuccess("KYC document rejected.");
    },
    onError,
  });

  const backgroundCheckMutation = useMutation({
    mutationFn: () =>
      recordBackgroundCheck(partnerId, { status: Number(bgStatus) as PartnerBackgroundCheckStatus, notes: bgNotes || undefined }),
    onSuccess: () => {
      setBgNotes("");
      onSuccess("Background check recorded.");
    },
    onError,
  });

  const adjustmentMutation = useMutation({
    mutationFn: () =>
      recordEarningAdjustment(partnerId, {
        entryType: Number(adjustmentType) as PartnerEarningEntryType,
        amount: Number(adjustmentAmount),
        sourceType: PartnerEarningSourceType.ManualAdjustment,
        description: adjustmentDescription,
      }),
    onSuccess: () => {
      setAdjustmentAmount("");
      setAdjustmentDescription("");
      onSuccess("Earning ledger adjustment recorded.");
    },
    onError,
  });

  const createPayoutMutation = useMutation({
    mutationFn: () => createPayoutBatch(partnerId, { periodStart: payoutPeriodStart, periodEnd: payoutPeriodEnd }),
    onSuccess: () => {
      setPayoutPeriodStart("");
      setPayoutPeriodEnd("");
      onSuccess("Payout batch created.");
    },
    onError,
  });

  const payoutStatusMutation = useMutation({
    mutationFn: ({ payoutId, status, payoutReference }: { payoutId: string; status: PartnerPayoutStatus; payoutReference?: string }) =>
      updatePayoutStatus(payoutId, { status, payoutReference }),
    onSuccess: () => {
      setPendingPayoutFailure(null);
      onSuccess("Payout status updated.");
    },
    onError,
  });

  const breadcrumbs = [
    { label: "Partners", href: "/partners" },
    { label: detailQuery.data?.displayName ?? "Partner" },
  ];

  if (detailQuery.isPending) {
    return <DetailSkeleton cards={4} className="mx-auto flex w-full max-w-4xl flex-col gap-6" />;
  }

  if (detailQuery.isError) {
    return (
      <DetailError
        title="Partner"
        breadcrumbs={breadcrumbs}
        error={detailQuery.error}
        onRetry={() => detailQuery.refetch()}
        className="mx-auto w-full max-w-4xl"
      />
    );
  }

  const partner = detailQuery.data;
  const canActivate =
    partner.status === PartnerStatus.PendingVerification &&
    (partner.onboardingStatus === PartnerOnboardingStatus.KycVerified || partner.onboardingStatus === PartnerOnboardingStatus.Completed);

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-6">
      <PageHeading
        title={partner.displayName}
        subtitle={`${partner.phone}${partner.email ? ` · ${partner.email}` : ""}`}
        breadcrumbs={<Breadcrumbs items={breadcrumbs} />}
        actions={
          <div className="flex flex-wrap items-center gap-2">
            <PartnerStatusBadge status={partner.status} label={STATUS_LABELS[partner.status]} />
            <Badge
              tone={partner.onboardingStatus === PartnerOnboardingStatus.Completed ? "success" : "neutral"}
            >
              {ONBOARDING_LABELS[partner.onboardingStatus]}
            </Badge>
          </div>
        }
      />

      {actionError ? <Alert tone="error">{actionError}</Alert> : null}
      {actionNotice ? <Alert tone="success">{actionNotice}</Alert> : null}

      <Card
        title="Profile"
        description={
          canWritePartner
            ? "Name and email save together; status changes below apply immediately."
            : "Read-only — you do not hold partner write access."
        }
      >
        {canWritePartner ? (
          // Keyed on the partner id so the editor re-seeds from the server if
          // this screen is ever navigated to a different partner without
          // unmounting. It owns its own state (see ProfileEditor) rather than
          // writing into page state on change: the previous version tracked
          // each field in state initialised to "" and submitted all three, so
          // editing only the display name silently blanked the legal name.
          <ProfileEditor
            key={partner.id}
            partner={partner}
            saving={updateMutation.isPending}
            onSave={(values) => updateMutation.mutate(values)}
          />
        ) : (
          <p className="text-sm text-fg-muted">
            Legal name: <span className="text-fg">{partner.legalName}</span>
          </p>
        )}

        {canWritePartner ? (
          <div className="mt-5 flex flex-col gap-4 border-t border-line pt-5">
            <FormActions align="start">
              {canActivate ? (
                <Button loading={activateMutation.isPending} onClick={() => activateMutation.mutate()}>
                  Activate partner
                </Button>
              ) : null}
              {partner.status === PartnerStatus.Suspended ? (
                <Button loading={reactivateMutation.isPending} onClick={() => reactivateMutation.mutate()}>
                  Reactivate
                </Button>
              ) : null}
            </FormActions>

            {partner.status !== PartnerStatus.Suspended ? (
              <div className="flex flex-col gap-3 sm:flex-row sm:items-end">
                <div className="flex-1">
                  <Field
                    label="Suspend reason"
                    required
                    value={suspendReason}
                    onChange={(e) => setSuspendReason(e.target.value)}
                    hint="Recorded to the audit trail and shown to the partner."
                  />
                </div>
                <Button variant="danger" disabled={!suspendReason.trim()} onClick={() => setConfirmSuspend(true)}>
                  Suspend partner
                </Button>
              </div>
            ) : null}
          </div>
        ) : null}
      </Card>

      <Card title="KYC documents" description="Approve or reject each submitted document (task 150b)">
        {partner.kycDocuments.length === 0 ? (
          <EmptyState
            title="No KYC documents submitted yet"
            description="The partner submits these from the partner app; activation is gated on them."
          />
        ) : (
          <ul className="flex flex-col gap-3 text-sm">
            {partner.kycDocuments.map((doc) => (
              <li key={doc.id} className="rounded-xl border border-line p-3">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <span className="font-medium text-fg">{KYC_DOC_TYPE_LABELS[doc.docType]}</span>
                  <Badge tone={KYC_STATUS_TONES[doc.verificationStatus]}>
                    {KYC_STATUS_LABELS[doc.verificationStatus]}
                  </Badge>
                </div>
                <p className="mt-1 text-xs text-fg-subtle">
                  {doc.docNumber ? (
                    <>
                      Doc <span className="nums">#{doc.docNumber}</span> ·{" "}
                    </>
                  ) : null}
                  Submitted {formatDateTime(doc.submittedAt)}
                </p>

                {canWritePartner && doc.verificationStatus === PartnerKycVerificationStatus.Pending ? (
                  <div className="mt-3 flex flex-col gap-3 border-t border-line pt-3 sm:flex-row sm:items-end">
                    <Button
                      variant="secondary"
                      loading={approveKycMutation.isPending && approveKycMutation.variables === doc.id}
                      onClick={() => approveKycMutation.mutate(doc.id)}
                    >
                      Approve
                    </Button>
                    <div className="flex-1">
                      <Field
                        label="Rejection reason"
                        value={rejectReasonByDoc[doc.id] ?? ""}
                        onChange={(e) => setRejectReasonByDoc((m) => ({ ...m, [doc.id]: e.target.value }))}
                      />
                    </div>
                    <Button
                      variant="danger"
                      disabled={!(rejectReasonByDoc[doc.id] ?? "").trim()}
                      onClick={() =>
                        setPendingKycRejection({ id: doc.id, label: KYC_DOC_TYPE_LABELS[doc.docType] })
                      }
                    >
                      Reject
                    </Button>
                  </div>
                ) : null}
              </li>
            ))}
          </ul>
        )}
      </Card>

      <Card title="Background check" description="Distinct post-KYC step; required before activation (task 160)">
        {partner.backgroundChecks.length === 0 ? (
          <EmptyState
            title="No background check recorded yet"
            description={
              canWritePartner
                ? "Record the outcome below — a partner cannot be activated without one."
                : "An admin with partner write access records this."
            }
          />
        ) : (
          <ul className="flex flex-col gap-2 text-sm">
            {partner.backgroundChecks.map((check) => (
              <li key={check.id} className="rounded-xl border border-line p-3">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <Badge tone={BACKGROUND_CHECK_TONES[check.status]}>
                    {BACKGROUND_CHECK_STATUS_LABELS[check.status]}
                  </Badge>
                  <span className="text-xs text-fg-subtle">{formatDateTime(check.checkedAt)}</span>
                </div>
                {check.notes ? <p className="mt-1.5 text-xs text-fg-muted">{check.notes}</p> : null}
              </li>
            ))}
          </ul>
        )}

        {canWritePartner ? (
          <div className="mt-5 flex flex-col gap-3 border-t border-line pt-5 sm:flex-row sm:items-end">
            <Select
              label="Outcome"
              value={bgStatus}
              onChange={(e) => setBgStatus(e.target.value)}
              options={[
                { value: String(PartnerBackgroundCheckStatus.Passed), label: "Passed" },
                { value: String(PartnerBackgroundCheckStatus.Failed), label: "Failed" },
              ]}
            />
            <div className="flex-1">
              <Field label="Notes (optional)" value={bgNotes} onChange={(e) => setBgNotes(e.target.value)} />
            </div>
            <Button loading={backgroundCheckMutation.isPending} onClick={() => backgroundCheckMutation.mutate()}>
              Record outcome
            </Button>
          </div>
        ) : null}
      </Card>

      <Card title="Performance" description="Job-fulfilment summary (task 150c)">
        {performanceQuery.isPending ? (
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
            {Array.from({ length: 6 }, (_, index) => (
              <div key={index} className="rounded-2xl border border-line bg-surface p-5">
                <SkeletonText lines={2} />
              </div>
            ))}
          </div>
        ) : performanceQuery.isError ? (
          <SectionError error={performanceQuery.error} onRetry={() => performanceQuery.refetch()} />
        ) : (
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
            <StatTile label="Total assignments" value={String(performanceQuery.data.totalAssignments)} />
            <StatTile label="Accepted" value={String(performanceQuery.data.acceptedAssignments)} />
            <StatTile label="Rejected" value={String(performanceQuery.data.rejectedAssignments)} />
            <StatTile label="Completed jobs" value={String(performanceQuery.data.completedJobs)} />
            <StatTile label="In-progress jobs" value={String(performanceQuery.data.inProgressJobs)} />
            <StatTile label="Lifetime earnings" value={formatCurrency(performanceQuery.data.lifetimeEarnings)} />
          </div>
        )}
      </Card>

      <Card
        title="Earnings ledger"
        description={
          earningsQuery.data
            ? `Current balance: ${formatCurrency(earningsQuery.data.currentBalance)}`
            : "Append-only ledger (task 148)"
        }
      >
        {earningsQuery.isPending ? (
          <SkeletonText lines={4} />
        ) : earningsQuery.isError ? (
          <SectionError error={earningsQuery.error} onRetry={() => earningsQuery.refetch()} />
        ) : earningsQuery.data.entries.length === 0 ? (
          <EmptyState title="No earning activity yet" description="Completed jobs credit this ledger automatically." />
        ) : (
          <ul className="flex flex-col gap-2 text-sm">
            {earningsQuery.data.entries.map((entry) => (
              <li
                key={entry.id}
                className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-line p-3"
              >
                <span className="min-w-0 flex-1 text-fg">{entry.description}</span>
                <span className="nums text-xs text-fg-subtle">{formatDate(entry.createdAtUtc)}</span>
                <span
                  className={
                    entry.entryType === PartnerEarningEntryType.Credit
                      ? "nums font-medium text-success"
                      : "nums font-medium text-danger"
                  }
                >
                  {entry.entryType === PartnerEarningEntryType.Credit ? "+" : "−"}
                  {formatCurrency(entry.amount)}
                </span>
              </li>
            ))}
          </ul>
        )}

        {canWritePayout ? (
          <div className="mt-5 flex flex-col gap-3 border-t border-line pt-5 sm:flex-row sm:items-end">
            <Select
              label="Type"
              value={adjustmentType}
              onChange={(e) => setAdjustmentType(e.target.value)}
              options={[
                { value: String(PartnerEarningEntryType.Credit), label: "Credit" },
                { value: String(PartnerEarningEntryType.Debit), label: "Debit (penalty)" },
              ]}
            />
            <Field
              label="Amount"
              type="number"
              min="0.01"
              step="0.01"
              leading="₹"
              value={adjustmentAmount}
              onChange={(e) => setAdjustmentAmount(e.target.value)}
            />
            <div className="flex-1">
              <Field label="Description" value={adjustmentDescription} onChange={(e) => setAdjustmentDescription(e.target.value)} />
            </div>
            <Button
              disabled={!adjustmentAmount || !adjustmentDescription.trim()}
              loading={adjustmentMutation.isPending}
              onClick={() => adjustmentMutation.mutate()}
            >
              Record adjustment
            </Button>
          </div>
        ) : null}
      </Card>

      <Card title="Payouts" description="Manual bank-transfer payout batches (OPEN DECISIONS #3, task 148)">
        {payoutsQuery.isPending ? (
          <SkeletonText lines={4} />
        ) : payoutsQuery.isError ? (
          <SectionError error={payoutsQuery.error} onRetry={() => payoutsQuery.refetch()} />
        ) : payoutsQuery.data.items.length === 0 ? (
          <EmptyState
            title="No payout batches yet"
            description={
              canWritePayout
                ? "Run a batch below to settle this partner's outstanding balance."
                : "An admin with payout write access can run one."
            }
          />
        ) : (
          <ul className="flex flex-col gap-3 text-sm">
            {payoutsQuery.data.items.map((payout) => (
              <li key={payout.id} className="rounded-xl border border-line p-3">
                <div className="flex flex-wrap items-center justify-between gap-2">
                  <span className="nums font-medium text-fg">
                    {payout.periodStart} → {payout.periodEnd}
                  </span>
                  <Badge tone={PAYOUT_STATUS_TONES[payout.status]}>{PAYOUT_STATUS_LABELS[payout.status]}</Badge>
                </div>
                <p className="nums mt-1 text-fg">{formatCurrency(payout.totalAmount)}</p>
                {payout.payoutReference ? (
                  <p className="mt-1 text-xs text-fg-subtle">Reference: {payout.payoutReference}</p>
                ) : null}

                {canWritePayout && payout.status === PartnerPayoutStatus.Pending ? (
                  <FormActions align="start" className="mt-3">
                    <Button
                      size="sm"
                      variant="secondary"
                      loading={
                        payoutStatusMutation.isPending && payoutStatusMutation.variables?.payoutId === payout.id
                      }
                      onClick={() => payoutStatusMutation.mutate({ payoutId: payout.id, status: PartnerPayoutStatus.Processing })}
                    >
                      Mark processing
                    </Button>
                  </FormActions>
                ) : null}

                {canWritePayout && payout.status === PartnerPayoutStatus.Processing ? (
                  <div className="mt-3 flex flex-col gap-3 border-t border-line pt-3 sm:flex-row sm:items-end">
                    <div className="flex-1">
                      <Field
                        label="Bank transfer reference"
                        value={payoutReferenceByPayout[payout.id] ?? ""}
                        onChange={(e) => setPayoutReferenceByPayout((m) => ({ ...m, [payout.id]: e.target.value }))}
                      />
                    </div>
                    <Button
                      disabled={!(payoutReferenceByPayout[payout.id] ?? "").trim()}
                      loading={
                        payoutStatusMutation.isPending &&
                        payoutStatusMutation.variables?.payoutId === payout.id &&
                        payoutStatusMutation.variables?.status === PartnerPayoutStatus.Paid
                      }
                      onClick={() =>
                        payoutStatusMutation.mutate({
                          payoutId: payout.id,
                          status: PartnerPayoutStatus.Paid,
                          payoutReference: (payoutReferenceByPayout[payout.id] ?? "").trim(),
                        })
                      }
                    >
                      Mark paid
                    </Button>
                    <Button variant="danger" onClick={() => setPendingPayoutFailure(payout.id)}>
                      Mark failed
                    </Button>
                  </div>
                ) : null}
              </li>
            ))}
          </ul>
        )}

        {canWritePayout ? (
          <div className="mt-5 flex flex-col gap-3 border-t border-line pt-5 sm:flex-row sm:items-end">
            <Field label="Period start" type="date" value={payoutPeriodStart} onChange={(e) => setPayoutPeriodStart(e.target.value)} />
            <Field label="Period end" type="date" value={payoutPeriodEnd} onChange={(e) => setPayoutPeriodEnd(e.target.value)} />
            <Button
              disabled={!payoutPeriodStart || !payoutPeriodEnd}
              loading={createPayoutMutation.isPending}
              onClick={() => createPayoutMutation.mutate()}
            >
              Run payout batch
            </Button>
          </div>
        ) : null}
      </Card>

      <ConfirmDialog
        open={confirmSuspend}
        title="Suspend this partner?"
        description="They are removed from assignment immediately and cannot accept new jobs."
        confirmLabel="Suspend partner"
        cancelLabel="Keep active"
        loading={suspendMutation.isPending}
        error={suspendMutation.isError ? describeError(suspendMutation.error) : null}
        onCancel={() => setConfirmSuspend(false)}
        onConfirm={() => suspendMutation.mutate()}
      >
        <p className="text-sm text-fg-muted">
          Reason: <span className="font-medium text-fg">{suspendReason}</span>
        </p>
      </ConfirmDialog>

      <ConfirmDialog
        open={pendingKycRejection !== null}
        title="Reject this KYC document?"
        description="The partner must resubmit before they can be activated."
        confirmLabel="Reject document"
        cancelLabel="Keep pending"
        loading={rejectKycMutation.isPending}
        error={rejectKycMutation.isError ? describeError(rejectKycMutation.error) : null}
        onCancel={() => setPendingKycRejection(null)}
        onConfirm={() => {
          if (!pendingKycRejection) return;
          rejectKycMutation.mutate({
            documentId: pendingKycRejection.id,
            reason: (rejectReasonByDoc[pendingKycRejection.id] ?? "").trim(),
          });
        }}
      >
        {pendingKycRejection ? (
          <p className="text-sm text-fg-muted">
            {pendingKycRejection.label} —{" "}
            <span className="font-medium text-fg">{rejectReasonByDoc[pendingKycRejection.id] ?? ""}</span>
          </p>
        ) : null}
      </ConfirmDialog>

      <ConfirmDialog
        open={pendingPayoutFailure !== null}
        title="Mark this payout failed?"
        description="The batch is closed as failed and the amount stays owed to the partner."
        confirmLabel="Mark failed"
        cancelLabel="Keep processing"
        loading={payoutStatusMutation.isPending}
        error={payoutStatusMutation.isError ? describeError(payoutStatusMutation.error) : null}
        onCancel={() => setPendingPayoutFailure(null)}
        onConfirm={() => {
          if (!pendingPayoutFailure) return;
          payoutStatusMutation.mutate({ payoutId: pendingPayoutFailure, status: PartnerPayoutStatus.Failed });
        }}
      />
    </div>
  );
}

/**
 * Profile name/email editor.
 *
 * Owns its own state, seeded from the loaded partner. The previous version
 * kept the three fields in page state initialised to `""` and rendered them
 * with `defaultValue`, then submitted all three on save — so an admin who
 * changed only the display name saved an empty legal name and email over the
 * real ones.
 */
function ProfileEditor({
  partner,
  saving,
  onSave,
}: {
  partner: PartnerDetail;
  saving: boolean;
  onSave: (values: { legalName: string; displayName: string; email: string }) => void;
}) {
  const [legalName, setLegalName] = useState(partner.legalName);
  const [displayName, setDisplayName] = useState(partner.displayName);
  const [email, setEmail] = useState(partner.email ?? "");

  return (
    <div className="flex flex-col gap-4">
      <FormGrid columns={3}>
        <Field label="Legal name" required value={legalName} onChange={(e) => setLegalName(e.target.value)} />
        <Field label="Display name" required value={displayName} onChange={(e) => setDisplayName(e.target.value)} />
        <Field label="Email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} />
      </FormGrid>
      <FormActions align="start">
        <Button
          variant="secondary"
          disabled={!legalName.trim() || !displayName.trim()}
          loading={saving}
          onClick={() => onSave({ legalName, displayName, email })}
        >
          Save profile
        </Button>
      </FormActions>
    </div>
  );
}
