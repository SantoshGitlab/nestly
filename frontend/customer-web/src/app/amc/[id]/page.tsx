"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useState } from "react";
import {
  BannerBreadcrumb,
  DetailList,
  DetailRow,
  ScreenSkeleton,
  formatInstant,
  formatInstantDate,
  inr,
} from "@/components/patterns";
import { PageBanner } from "@/components/PageBanner";
import { RequireAuth } from "@/components/RequireAuth";
import {
  Alert,
  Badge,
  Button,
  Card,
  LinkButton,
  Modal,
  useToast,
} from "@/components/ui";
import type { BadgeTone } from "@/components/ui";
import { describeError } from "@/lib/api";
import { cancelMyAmcContract, getMyAmcContract } from "@/lib/amc-api";
import { CustomerAmcContractStatus } from "@/lib/types";

function statusLabel(status: CustomerAmcContractStatus): string {
  switch (status) {
    case CustomerAmcContractStatus.Active:
      return "Active";
    case CustomerAmcContractStatus.Exhausted:
      return "All visits used";
    case CustomerAmcContractStatus.Expired:
      return "Expired";
    case CustomerAmcContractStatus.Cancelled:
      return "Cancelled";
    default:
      return "Unknown";
  }
}

function statusTone(status: CustomerAmcContractStatus): BadgeTone {
  return status === CustomerAmcContractStatus.Active ? "success" : "neutral";
}

/** One AMC contract's full detail: term, remaining entitlement, visit history and cancel (docs/AMC.md). */
export default function AmcContractDetailPage() {
  return (
    <RequireAuth>
      <ContractDetailScreen />
    </RequireAuth>
  );
}

function ContractDetailScreen() {
  const { id: contractId } = useParams<{ id: string }>();
  const queryClient = useQueryClient();
  const toast = useToast();
  const [confirmOpen, setConfirmOpen] = useState(false);

  const query = useQuery({
    queryKey: ["amc-contract", contractId],
    queryFn: () => getMyAmcContract(contractId),
  });

  const cancelMutation = useMutation({
    mutationFn: () => cancelMyAmcContract(contractId),
    onSuccess: () => {
      setConfirmOpen(false);
      queryClient.invalidateQueries({ queryKey: ["amc-contract", contractId] });
      queryClient.invalidateQueries({ queryKey: ["my-amc-contracts"] });
      toast("success", "Contract cancelled.");
    },
  });

  if (query.isPending) {
    return (
      <main className="flex w-full flex-col" aria-hidden>
        <div className="listing-banner h-[13.5rem] w-full sm:h-[15.5rem]" />
        <ScreenSkeleton cards={3} className="mx-auto w-full max-w-7xl px-4 py-10 sm:px-6 sm:py-14" />
      </main>
    );
  }

  if (query.isError) {
    return (
      <main className="mx-auto w-full max-w-7xl px-4 py-8 sm:px-6 sm:py-12">
        <Alert
          tone="error"
          title="Couldn't load this contract"
          action={
            <Button size="sm" variant="secondary" onClick={() => query.refetch()}>
              Retry
            </Button>
          }
        >
          {describeError(query.error)}
        </Alert>
      </main>
    );
  }

  const contract = query.data;
  const canCancel =
    contract.status === CustomerAmcContractStatus.Active ||
    contract.status === CustomerAmcContractStatus.Exhausted;

  return (
    <main className="flex w-full flex-col animate-rise">
      <PageBanner
        title={contract.assetLabel}
        description={`${contract.planName} · ${contract.categoryName}`}
        badge={<Badge tone={statusTone(contract.status)}>{statusLabel(contract.status)}</Badge>}
        breadcrumb={
          <BannerBreadcrumb
            items={[{ label: "Home", href: "/" }, { label: "My AMC contracts", href: "/amc" }, { label: contract.assetLabel }]}
          />
        }
      />

      <div className="mx-auto w-full max-w-7xl px-4 py-10 sm:px-6 sm:py-14">
      <div className="flex flex-col gap-6">
        <Card title="Cover" description="What this contract includes.">
          <DetailList>
            <DetailRow label="Price paid" numeric>
              {inr(contract.price)}
            </DetailRow>
            <DetailRow label="Visits remaining" numeric>
              {contract.visitsRemaining} of {contract.visitsIncluded}
            </DetailRow>
            <DetailRow label="Term" numeric>
              {contract.termMonths} month{contract.termMonths === 1 ? "" : "s"}
            </DetailRow>
            <DetailRow label="Cover period" numeric>
              {formatInstantDate(contract.startDateUtc)} – {formatInstantDate(contract.endDateUtc)}
            </DetailRow>
            <DetailRow label="Purchased" numeric>
              {formatInstantDate(contract.createdAtUtc)}
            </DetailRow>
            {contract.cancelledAtUtc ? (
              <DetailRow label="Cancelled on" numeric>
                {formatInstantDate(contract.cancelledAtUtc)}
              </DetailRow>
            ) : null}
          </DetailList>
        </Card>

        {contract.canRedeemNow ? (
          <Card
            title="Redeem a visit"
            description="Book a service visit against this contract — no charge, it's already paid for."
          >
            <LinkButton href={`/amc/${contract.id}/redeem`}>Redeem a visit</LinkButton>
          </Card>
        ) : null}

        <Card
          title="Visit history"
          description={
            contract.visits.length === 0
              ? "No visits redeemed yet."
              : `${contract.visits.length} visit${contract.visits.length === 1 ? "" : "s"} redeemed so far.`
          }
        >
          {contract.visits.length === 0 ? (
            <p className="text-sm text-fg-subtle">Nothing to show yet.</p>
          ) : (
            <ul className="flex flex-col divide-y divide-line">
              {contract.visits.map((visit) => (
                <li key={visit.id} className="flex items-center justify-between gap-3 py-3 first:pt-0 last:pb-0">
                  <span className="text-sm text-fg-muted">
                    Redeemed <span className="nums">{formatInstant(visit.consumedAtUtc)}</span>
                  </span>
                  <Link
                    href={`/bookings/${visit.bookingId}`}
                    className="shrink-0 text-sm font-medium text-brand-600 underline-offset-4 hover:underline dark:text-brand-400"
                  >
                    View booking
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </Card>

        {canCancel ? (
          <Card
            title="Cancel contract"
            description="Any visits you haven't redeemed yet are forfeited. This can't be undone."
          >
            {cancelMutation.isError ? (
              <div className="mb-4">
                <Alert tone="error" title="Couldn't cancel this contract">
                  {describeError(cancelMutation.error)}
                </Alert>
              </div>
            ) : null}
            <Button type="button" variant="danger" onClick={() => setConfirmOpen(true)}>
              Cancel contract
            </Button>
          </Card>
        ) : null}
      </div>

      <Modal
        open={confirmOpen}
        onClose={() => {
          if (cancelMutation.isPending) return;
          setConfirmOpen(false);
        }}
        title="Cancel this contract?"
        description={`${contract.assetLabel} will no longer be covered by ${contract.planName}.`}
        size="sm"
        footer={
          <>
            <Button
              type="button"
              variant="secondary"
              disabled={cancelMutation.isPending}
              onClick={() => setConfirmOpen(false)}
            >
              Keep it
            </Button>
            <Button
              type="button"
              variant="danger"
              loading={cancelMutation.isPending}
              onClick={() => {
                if (cancelMutation.isPending) return;
                cancelMutation.mutate();
              }}
            >
              Yes, cancel it
            </Button>
          </>
        }
      >
        <p className="text-sm leading-relaxed text-fg-muted">
          You&apos;ll lose{" "}
          <span className="nums font-medium text-fg">{contract.visitsRemaining}</span> remaining visit
          {contract.visitsRemaining === 1 ? "" : "s"}. This can&apos;t be undone — buy a fresh plan any
          time from the AMC plans page.
        </p>
      </Modal>
      </div>
    </main>
  );
}
