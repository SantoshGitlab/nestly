"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useEffect, useState } from "react";
import { Alert, Button, Card, Field, PageHeading, Select } from "@/components/ui";
import { describeError } from "@/lib/api";
import { getSessionClaims, subscribeToAuthChanges } from "@/lib/auth";
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
import type { AdminSessionClaims } from "@/lib/types";

function useAdminClaims(): AdminSessionClaims | null {
  const [claims, setClaims] = useState<AdminSessionClaims | null>(null);
  useEffect(() => {
    const sync = () => setClaims(getSessionClaims());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);
  return claims;
}

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

const BACKGROUND_CHECK_STATUS_LABELS: Record<PartnerBackgroundCheckStatus, string> = {
  [PartnerBackgroundCheckStatus.Pending]: "Pending",
  [PartnerBackgroundCheckStatus.Passed]: "Passed",
  [PartnerBackgroundCheckStatus.Failed]: "Failed",
};

const PAYOUT_STATUS_LABELS: Record<PartnerPayoutStatus, string> = {
  [PartnerPayoutStatus.Pending]: "Pending",
  [PartnerPayoutStatus.Processing]: "Processing",
  [PartnerPayoutStatus.Paid]: "Paid",
  [PartnerPayoutStatus.Failed]: "Failed",
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

  const [editLegalName, setEditLegalName] = useState("");
  const [editDisplayName, setEditDisplayName] = useState("");
  const [editEmail, setEditEmail] = useState("");
  const [suspendReason, setSuspendReason] = useState("");

  const [rejectReasonByDoc, setRejectReasonByDoc] = useState<Record<string, string>>({});

  const [bgStatus, setBgStatus] = useState(String(PartnerBackgroundCheckStatus.Passed));
  const [bgNotes, setBgNotes] = useState("");

  const [adjustmentType, setAdjustmentType] = useState(String(PartnerEarningEntryType.Credit));
  const [adjustmentAmount, setAdjustmentAmount] = useState("");
  const [adjustmentDescription, setAdjustmentDescription] = useState("");

  const [payoutPeriodStart, setPayoutPeriodStart] = useState("");
  const [payoutPeriodEnd, setPayoutPeriodEnd] = useState("");
  const [payoutReferenceByPayout, setPayoutReferenceByPayout] = useState<Record<string, string>>({});

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
    mutationFn: () => updatePartner(partnerId, { legalName: editLegalName, displayName: editDisplayName, email: editEmail || undefined }),
    onSuccess: () => onSuccess("Profile updated."),
    onError,
  });

  const suspendMutation = useMutation({
    mutationFn: () => suspendPartner(partnerId, { reason: suspendReason }),
    onSuccess: () => {
      setSuspendReason("");
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
    onSuccess: () => onSuccess("KYC document rejected."),
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
    onSuccess: () => onSuccess("Payout status updated."),
    onError,
  });

  if (detailQuery.isPending) {
    return <p className="text-sm text-neutral-500">Loading partner…</p>;
  }

  if (detailQuery.isError) {
    return <Alert>{describeError(detailQuery.error)}</Alert>;
  }

  const partner = detailQuery.data;
  const canActivate =
    partner.status === PartnerStatus.PendingVerification &&
    (partner.onboardingStatus === PartnerOnboardingStatus.KycVerified || partner.onboardingStatus === PartnerOnboardingStatus.Completed);

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-6">
      <div className="flex items-center justify-between">
        <PageHeading title={partner.displayName} subtitle={`${partner.phone}${partner.email ? ` · ${partner.email}` : ""}`} />
        <Link href="/partners" className="text-sm underline-offset-2 hover:underline">
          Back to partners
        </Link>
      </div>

      {actionError ? <Alert>{actionError}</Alert> : null}
      {actionNotice ? <Alert tone="success">{actionNotice}</Alert> : null}

      <Card
        title="Profile"
        description={`Status: ${STATUS_LABELS[partner.status]} · Onboarding: ${ONBOARDING_LABELS[partner.onboardingStatus]}`}
      >
        {canWritePartner ? (
          <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
            <Field label="Legal name" defaultValue={partner.legalName} onChange={(e) => setEditLegalName(e.target.value)} />
            <Field label="Display name" defaultValue={partner.displayName} onChange={(e) => setEditDisplayName(e.target.value)} />
            <Field label="Email" defaultValue={partner.email ?? ""} onChange={(e) => setEditEmail(e.target.value)} />
          </div>
        ) : null}

        <div className="mt-4 flex flex-wrap items-center gap-3">
          {canWritePartner ? (
            <Button
              variant="secondary"
              disabled={updateMutation.isPending}
              onClick={() => {
                updateMutation.mutate();
              }}
            >
              {updateMutation.isPending ? "Saving…" : "Save profile"}
            </Button>
          ) : null}

          {canWritePartner && partner.status === PartnerStatus.PendingVerification && canActivate ? (
            <Button disabled={activateMutation.isPending} onClick={() => activateMutation.mutate()}>
              {activateMutation.isPending ? "Activating…" : "Activate partner"}
            </Button>
          ) : null}

          {canWritePartner && partner.status === PartnerStatus.Suspended ? (
            <Button disabled={reactivateMutation.isPending} onClick={() => reactivateMutation.mutate()}>
              {reactivateMutation.isPending ? "Reactivating…" : "Reactivate"}
            </Button>
          ) : null}
        </div>

        {canWritePartner && partner.status !== PartnerStatus.Suspended ? (
          <div className="mt-4 flex flex-col gap-2 border-t border-black/10 pt-4 dark:border-white/15 sm:flex-row sm:items-end">
            <div className="flex-1">
              <Field label="Suspend reason" value={suspendReason} onChange={(e) => setSuspendReason(e.target.value)} />
            </div>
            <Button variant="danger" disabled={!suspendReason.trim() || suspendMutation.isPending} onClick={() => suspendMutation.mutate()}>
              {suspendMutation.isPending ? "Suspending…" : "Suspend partner"}
            </Button>
          </div>
        ) : null}
      </Card>

      <Card title="KYC documents" description="Approve or reject each submitted document (task 150b)">
        {partner.kycDocuments.length === 0 ? (
          <p className="text-sm text-neutral-500">No KYC documents submitted yet.</p>
        ) : (
          <ul className="flex flex-col gap-3 text-sm">
            {partner.kycDocuments.map((doc) => (
              <li key={doc.id} className="rounded-lg border border-black/10 p-3 dark:border-white/15">
                <div className="flex items-center justify-between">
                  <span className="font-medium">{KYC_DOC_TYPE_LABELS[doc.docType]}</span>
                  <span>{KYC_STATUS_LABELS[doc.verificationStatus]}</span>
                </div>
                <p className="mt-1 text-xs text-neutral-500">
                  {doc.docNumber ? `Doc #${doc.docNumber} · ` : ""}Submitted {new Date(doc.submittedAt).toLocaleString()}
                </p>

                {canWritePartner && doc.verificationStatus === PartnerKycVerificationStatus.Pending ? (
                  <div className="mt-3 flex flex-col gap-2 sm:flex-row sm:items-end">
                    <Button
                      variant="secondary"
                      disabled={approveKycMutation.isPending}
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
                      disabled={!(rejectReasonByDoc[doc.id] ?? "").trim() || rejectKycMutation.isPending}
                      onClick={() => rejectKycMutation.mutate({ documentId: doc.id, reason: (rejectReasonByDoc[doc.id] ?? "").trim() })}
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
          <p className="text-sm text-neutral-500">No background check recorded yet.</p>
        ) : (
          <ul className="flex flex-col gap-2 text-sm">
            {partner.backgroundChecks.map((check) => (
              <li key={check.id} className="rounded-lg border border-black/10 p-3 dark:border-white/15">
                <div className="flex items-center justify-between">
                  <span className="font-medium">{BACKGROUND_CHECK_STATUS_LABELS[check.status]}</span>
                  <span className="text-xs text-neutral-500">{new Date(check.checkedAt).toLocaleString()}</span>
                </div>
                {check.notes ? <p className="mt-1 text-xs text-neutral-600 dark:text-neutral-400">{check.notes}</p> : null}
              </li>
            ))}
          </ul>
        )}

        {canWritePartner ? (
          <div className="mt-4 flex flex-col gap-3 border-t border-black/10 pt-4 dark:border-white/15 sm:flex-row sm:items-end">
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
            <Button disabled={backgroundCheckMutation.isPending} onClick={() => backgroundCheckMutation.mutate()}>
              {backgroundCheckMutation.isPending ? "Recording…" : "Record outcome"}
            </Button>
          </div>
        ) : null}
      </Card>

      <Card title="Performance" description="Job-fulfilment summary (task 150c)">
        {performanceQuery.isPending ? (
          <p className="text-sm text-neutral-500">Loading…</p>
        ) : performanceQuery.isError ? (
          <Alert>{describeError(performanceQuery.error)}</Alert>
        ) : (
          <dl className="grid grid-cols-2 gap-x-4 gap-y-2 text-sm sm:grid-cols-3">
            <div>
              <dt className="text-neutral-500">Total assignments</dt>
              <dd>{performanceQuery.data.totalAssignments}</dd>
            </div>
            <div>
              <dt className="text-neutral-500">Accepted</dt>
              <dd>{performanceQuery.data.acceptedAssignments}</dd>
            </div>
            <div>
              <dt className="text-neutral-500">Rejected</dt>
              <dd>{performanceQuery.data.rejectedAssignments}</dd>
            </div>
            <div>
              <dt className="text-neutral-500">Completed jobs</dt>
              <dd>{performanceQuery.data.completedJobs}</dd>
            </div>
            <div>
              <dt className="text-neutral-500">In-progress jobs</dt>
              <dd>{performanceQuery.data.inProgressJobs}</dd>
            </div>
            <div>
              <dt className="text-neutral-500">Lifetime earnings</dt>
              <dd>₹{performanceQuery.data.lifetimeEarnings.toFixed(2)}</dd>
            </div>
          </dl>
        )}
      </Card>

      <Card
        title="Earnings ledger"
        description={earningsQuery.data ? `Current balance: ₹${earningsQuery.data.currentBalance.toFixed(2)}` : "Append-only ledger (task 148)"}
      >
        {earningsQuery.isPending ? (
          <p className="text-sm text-neutral-500">Loading…</p>
        ) : earningsQuery.isError ? (
          <Alert>{describeError(earningsQuery.error)}</Alert>
        ) : earningsQuery.data.entries.length === 0 ? (
          <p className="text-sm text-neutral-500">No earning activity yet.</p>
        ) : (
          <ul className="flex flex-col gap-2 text-sm">
            {earningsQuery.data.entries.map((entry) => (
              <li key={entry.id} className="flex items-center justify-between rounded-lg border border-black/10 p-3 dark:border-white/15">
                <span>{entry.description}</span>
                <span>{new Date(entry.createdAtUtc).toLocaleDateString()}</span>
                <span className={entry.entryType === PartnerEarningEntryType.Credit ? "text-green-700 dark:text-green-400" : "text-red-700 dark:text-red-400"}>
                  {entry.entryType === PartnerEarningEntryType.Credit ? "+" : "-"}₹{entry.amount.toFixed(2)}
                </span>
              </li>
            ))}
          </ul>
        )}

        {canWritePayout ? (
          <div className="mt-4 flex flex-col gap-3 border-t border-black/10 pt-4 dark:border-white/15 sm:flex-row sm:items-end">
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
              value={adjustmentAmount}
              onChange={(e) => setAdjustmentAmount(e.target.value)}
            />
            <div className="flex-1">
              <Field label="Description" value={adjustmentDescription} onChange={(e) => setAdjustmentDescription(e.target.value)} />
            </div>
            <Button
              disabled={!adjustmentAmount || !adjustmentDescription.trim() || adjustmentMutation.isPending}
              onClick={() => adjustmentMutation.mutate()}
            >
              {adjustmentMutation.isPending ? "Recording…" : "Record adjustment"}
            </Button>
          </div>
        ) : null}
      </Card>

      <Card title="Payouts" description="Manual bank-transfer payout batches (OPEN DECISIONS #3, task 148)">
        {payoutsQuery.isPending ? (
          <p className="text-sm text-neutral-500">Loading…</p>
        ) : payoutsQuery.isError ? (
          <Alert>{describeError(payoutsQuery.error)}</Alert>
        ) : payoutsQuery.data.items.length === 0 ? (
          <p className="text-sm text-neutral-500">No payout batches yet.</p>
        ) : (
          <ul className="flex flex-col gap-3 text-sm">
            {payoutsQuery.data.items.map((payout) => (
              <li key={payout.id} className="rounded-lg border border-black/10 p-3 dark:border-white/15">
                <div className="flex items-center justify-between">
                  <span className="font-medium">
                    {payout.periodStart} → {payout.periodEnd}
                  </span>
                  <span>{PAYOUT_STATUS_LABELS[payout.status]}</span>
                </div>
                <p className="mt-1 text-neutral-600 dark:text-neutral-400">₹{payout.totalAmount.toFixed(2)}</p>
                {payout.payoutReference ? <p className="mt-1 text-xs text-neutral-500">Reference: {payout.payoutReference}</p> : null}

                {canWritePayout && payout.status === PartnerPayoutStatus.Pending ? (
                  <Button
                    variant="secondary"
                    className="mt-2"
                    disabled={payoutStatusMutation.isPending}
                    onClick={() => payoutStatusMutation.mutate({ payoutId: payout.id, status: PartnerPayoutStatus.Processing })}
                  >
                    Mark processing
                  </Button>
                ) : null}

                {canWritePayout && payout.status === PartnerPayoutStatus.Processing ? (
                  <div className="mt-2 flex flex-col gap-2 sm:flex-row sm:items-end">
                    <div className="flex-1">
                      <Field
                        label="Bank transfer reference"
                        value={payoutReferenceByPayout[payout.id] ?? ""}
                        onChange={(e) => setPayoutReferenceByPayout((m) => ({ ...m, [payout.id]: e.target.value }))}
                      />
                    </div>
                    <Button
                      disabled={!(payoutReferenceByPayout[payout.id] ?? "").trim() || payoutStatusMutation.isPending}
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
                    <Button
                      variant="danger"
                      disabled={payoutStatusMutation.isPending}
                      onClick={() => payoutStatusMutation.mutate({ payoutId: payout.id, status: PartnerPayoutStatus.Failed })}
                    >
                      Mark failed
                    </Button>
                  </div>
                ) : null}
              </li>
            ))}
          </ul>
        )}

        {canWritePayout ? (
          <div className="mt-4 flex flex-col gap-3 border-t border-black/10 pt-4 dark:border-white/15 sm:flex-row sm:items-end">
            <Field label="Period start" type="date" value={payoutPeriodStart} onChange={(e) => setPayoutPeriodStart(e.target.value)} />
            <Field label="Period end" type="date" value={payoutPeriodEnd} onChange={(e) => setPayoutPeriodEnd(e.target.value)} />
            <Button
              disabled={!payoutPeriodStart || !payoutPeriodEnd || createPayoutMutation.isPending}
              onClick={() => createPayoutMutation.mutate()}
            >
              {createPayoutMutation.isPending ? "Running…" : "Run payout batch"}
            </Button>
          </div>
        ) : null}
      </Card>
    </div>
  );
}
