"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useParams } from "next/navigation";
import type { ReactNode } from "react";
import { ErrorState, NotYetAvailable } from "@/components/states";
import { Alert, Button, Card, PageHeading, Skeleton, SkeletonText } from "@/components/ui";
import { isNotImplemented } from "@/lib/api";
import { getPayoutDetail } from "@/lib/earnings-api";
import { PayoutStatus } from "@/lib/earnings-types";
import { formatDateTime, formatInr } from "@/lib/format";
import { PayoutStatusBadge } from "../../_components/PayoutStatusBadge";
import { periodLabel } from "../../_components/PayoutsSection";

/**
 * A single payout batch (docs/PROVIDER.md's `provider_payout` table). Carries
 * the same defensive HTTP 501 branch as the rest of the earnings surface, so
 * an older deployment still running the stub reads as "not built yet" rather
 * than as a failure.
 *
 * The amount is the reason this page is opened, so it is the page title
 * rather than a row in the list below it.
 */
export default function PayoutDetailPage() {
  const params = useParams<{ id: string }>();
  const payoutId = params.id;

  const query = useQuery({
    queryKey: ["provider-payout", payoutId],
    queryFn: () => getPayoutDetail(payoutId),
  });

  const backLink = (
    <Link
      href="/earnings"
      className="inline-flex items-center gap-1.5 text-sm font-medium text-fg-muted transition-colors duration-fast ease-out hover:text-fg"
    >
      <svg
        viewBox="0 0 24 24"
        fill="none"
        stroke="currentColor"
        strokeWidth="2"
        strokeLinecap="round"
        strokeLinejoin="round"
        className="h-4 w-4"
        aria-hidden
      >
        <path d="m15 18-6-6 6-6" />
      </svg>
      Earnings
    </Link>
  );

  if (query.isPending) {
    return <PayoutDetailSkeleton backLink={backLink} />;
  }

  if (query.isError && isNotImplemented(query.error)) {
    return (
      <div className="mx-auto w-full max-w-2xl">
        <PageHeading title="Payout" breadcrumbs={backLink} />
        <NotYetAvailable
          title="This payout isn't available yet"
          description="Payout batches are still being built on the platform side. Check back once payouts go live."
          action={
            <Link href="/earnings">
              <Button variant="secondary">Back to earnings</Button>
            </Link>
          }
        />
      </div>
    );
  }

  if (query.isError) {
    return (
      <div className="mx-auto w-full max-w-2xl">
        <PageHeading title="Payout" breadcrumbs={backLink} />
        <ErrorState
          title="Couldn't load this payout"
          error={query.error}
          onRetry={() => query.refetch()}
          isRetrying={query.isRefetching}
        />
      </div>
    );
  }

  const payout = query.data;

  return (
    <div className="mx-auto w-full max-w-2xl">
      <PageHeading
        breadcrumbs={backLink}
        title={formatInr(payout.totalAmount)}
        subtitle={`For work between ${periodLabel(payout)}`}
        actions={<PayoutStatusBadge status={payout.status} />}
      />

      {payout.status === PayoutStatus.Failed ? (
        <div className="mb-6">
          <Alert tone="error" title="This transfer didn't go through">
            {payout.notes ??
              "Check that your bank details on file are correct — support will retry this payout."}
          </Alert>
        </div>
      ) : null}

      <Card title="Payout details">
        <dl className="flex flex-col gap-4">
          <DetailRow label="Amount">
            <span className="nums text-base font-semibold text-fg">
              {formatInr(payout.totalAmount)}
            </span>
          </DetailRow>
          <DetailRow label="Period">
            <span className="nums">{periodLabel(payout)}</span>
          </DetailRow>
          <DetailRow label="Status">
            <PayoutStatusBadge status={payout.status} />
          </DetailRow>
          <DetailRow label="Payout reference">
            {payout.payoutReference ? (
              <span className="nums break-all">{payout.payoutReference}</span>
            ) : (
              <span className="text-fg-muted">
                Not issued yet — it appears here once the transfer is sent.
              </span>
            )}
          </DetailRow>
          <DetailRow label="Raised">
            <span className="nums">{formatDateTime(payout.createdAt)}</span>
          </DetailRow>
          {payout.notes && payout.status !== PayoutStatus.Failed ? (
            <DetailRow label="Notes">
              <span className="leading-relaxed">{payout.notes}</span>
            </DetailRow>
          ) : null}
        </dl>
      </Card>
    </div>
  );
}

/** One label/value pair, stacked so it survives a narrow screen. */
function DetailRow({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-1">
      <dt className="text-xs font-semibold uppercase tracking-wide text-fg-muted">{label}</dt>
      <dd className="flex flex-col items-start text-sm text-fg">{children}</dd>
    </div>
  );
}

/** Mirrors the loaded screen's shape so nothing jumps when the payout lands. */
function PayoutDetailSkeleton({ backLink }: { backLink: ReactNode }) {
  return (
    <div className="mx-auto w-full max-w-2xl">
      <div className="mb-6">
        <div className="mb-2">{backLink}</div>
        <Skeleton className="h-9 w-44" />
        <Skeleton className="mt-2 h-4 w-64" />
      </div>
      <div className="rounded-2xl border border-line bg-surface p-6 shadow-sm">
        <Skeleton className="h-5 w-32" />
        <SkeletonText lines={5} className="mt-5" />
      </div>
    </div>
  );
}
