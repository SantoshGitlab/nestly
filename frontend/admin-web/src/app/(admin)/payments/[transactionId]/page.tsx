"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useParams } from "next/navigation";
import { Badge, Card, EmptyState, PageHeading } from "@/components/ui";
import type { BadgeTone } from "@/components/ui";
import { Breadcrumbs, DescriptionList, formatCurrency, formatDateTime } from "@/components/data-table";
import { DetailError, DetailSkeleton } from "@/components/screen-states";
import { getPaymentTransactionDetail } from "@/lib/payments-api";
import {
  PaymentAttemptStatus,
  PaymentTransactionStatus,
  RefundMethod,
  RefundStatus,
  RefundType,
} from "@/lib/payments-types";

const STATUS_LABELS: Record<PaymentTransactionStatus, string> = {
  [PaymentTransactionStatus.Pending]: "Pending",
  [PaymentTransactionStatus.Success]: "Success",
  [PaymentTransactionStatus.Failed]: "Failed",
  [PaymentTransactionStatus.Cancelled]: "Cancelled",
};

const STATUS_TONES: Record<PaymentTransactionStatus, BadgeTone> = {
  [PaymentTransactionStatus.Pending]: "warning",
  [PaymentTransactionStatus.Success]: "success",
  [PaymentTransactionStatus.Failed]: "danger",
  [PaymentTransactionStatus.Cancelled]: "neutral",
};

const ATTEMPT_STATUS_LABELS: Record<PaymentAttemptStatus, string> = {
  [PaymentAttemptStatus.Created]: "Awaiting callback",
  [PaymentAttemptStatus.Success]: "Success",
  [PaymentAttemptStatus.Failed]: "Failed",
};

const ATTEMPT_STATUS_TONES: Record<PaymentAttemptStatus, BadgeTone> = {
  [PaymentAttemptStatus.Created]: "warning",
  [PaymentAttemptStatus.Success]: "success",
  [PaymentAttemptStatus.Failed]: "danger",
};

// Mirrors bookings/[bookingId]/page.tsx's identical maps - each admin-web
// detail screen that shows refunds defines its own labels locally rather
// than sharing a module, the same convention BOOKING_STATUS_LABELS already
// follows in two files.
const REFUND_STATUS_LABELS: Record<RefundStatus, string> = {
  [RefundStatus.Initiated]: "Initiated",
  [RefundStatus.Processing]: "Processing",
  [RefundStatus.Refunded]: "Refunded",
  [RefundStatus.Failed]: "Failed",
};

const REFUND_STATUS_TONES: Record<RefundStatus, BadgeTone> = {
  [RefundStatus.Initiated]: "info",
  [RefundStatus.Processing]: "warning",
  [RefundStatus.Refunded]: "success",
  [RefundStatus.Failed]: "danger",
};

const REFUND_METHOD_LABELS: Record<RefundMethod, string> = {
  [RefundMethod.Gateway]: "Gateway",
  [RefundMethod.Wallet]: "Wallet credit",
};

const REFUND_TYPE_LABELS: Record<RefundType, string> = {
  [RefundType.Full]: "Full",
  [RefundType.Partial]: "Partial",
};

/**
 * Admin payment transaction detail (SRS 12.13.1, 14.3, task 311): every
 * gateway round-trip and every refund raised against one transaction -
 * `PaymentsController.GetDetail` (admin-api). A transaction id that does not
 * exist 404s (see the controller's doc comment), which `DetailError` here
 * renders the same way every other admin detail screen's 404 renders.
 */
export default function PaymentTransactionDetailPage() {
  const params = useParams<{ transactionId: string }>();
  const transactionId = params.transactionId;

  const detailQuery = useQuery({
    queryKey: ["admin-payment-detail", transactionId],
    queryFn: () => getPaymentTransactionDetail(transactionId),
  });

  const breadcrumbs = [
    { label: "Payments", href: "/payments" },
    { label: transactionId.slice(0, 8) },
  ];

  if (detailQuery.isPending) {
    return <DetailSkeleton cards={3} className="mx-auto flex w-full max-w-4xl flex-col gap-6" />;
  }

  if (detailQuery.isError) {
    return (
      <DetailError
        title="Payment transaction"
        breadcrumbs={breadcrumbs}
        error={detailQuery.error}
        onRetry={() => detailQuery.refetch()}
        className="mx-auto w-full max-w-4xl"
      />
    );
  }

  const transaction = detailQuery.data;

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-6">
      <PageHeading
        title={`${transaction.currency} ${formatCurrency(transaction.amount)}`}
        subtitle={transaction.id}
        breadcrumbs={<Breadcrumbs items={breadcrumbs} />}
        actions={<Badge tone={STATUS_TONES[transaction.status]}>{STATUS_LABELS[transaction.status]}</Badge>}
      />

      <Card title="Transaction" description="SRS 12.13.1">
        <DescriptionList
          columns={3}
          items={[
            {
              label: "Booking",
              value: (
                <Link href={`/bookings/${transaction.bookingId}`} className="text-brand-600 hover:underline dark:text-brand-400">
                  {transaction.bookingId}
                </Link>
              ),
            },
            { label: "Customer ID", value: <span className="nums break-all">{transaction.customerId}</span> },
            {
              label: "Commission",
              value:
                transaction.commissionAmount !== null
                  ? `${formatCurrency(transaction.commissionAmount)} (${transaction.commissionRatePercentage ?? 0}%)`
                  : "—",
            },
            { label: "Created", value: formatDateTime(transaction.createdAtUtc) },
            { label: "Updated", value: formatDateTime(transaction.updatedAtUtc) },
          ]}
        />
      </Card>

      <Card title="Gateway attempts" description="Every order round-trip for this transaction (task 70 retries included)">
        {transaction.attempts.length === 0 ? (
          <EmptyState title="No attempts yet" description="No gateway order has been created for this transaction." />
        ) : (
          <ul className="flex flex-col gap-2 text-sm">
            {transaction.attempts.map((attempt) => (
              <li key={attempt.id} className="rounded-xl border border-line p-3">
                <div className="flex flex-wrap items-center justify-between gap-3">
                  <span className="text-fg">Attempt #{attempt.attemptNumber}</span>
                  <Badge tone={ATTEMPT_STATUS_TONES[attempt.status]}>{ATTEMPT_STATUS_LABELS[attempt.status]}</Badge>
                </div>
                <p className="nums mt-1 break-all text-xs text-fg-subtle">
                  Order: {attempt.gatewayOrderId}
                  {attempt.gatewayPaymentRef ? ` · Ref: ${attempt.gatewayPaymentRef}` : ""}
                </p>
                {attempt.failureReason ? (
                  <p className="mt-1 text-xs text-danger">{attempt.failureReason}</p>
                ) : null}
                <p className="mt-1 text-xs text-fg-subtle">
                  {formatDateTime(attempt.createdAtUtc)}
                  {attempt.completedAtUtc ? ` → ${formatDateTime(attempt.completedAtUtc)}` : ""}
                </p>
              </li>
            ))}
          </ul>
        )}
      </Card>

      <Card title="Refund history" description="SRS 14.4 reconciliation trail">
        {transaction.refunds.length === 0 ? (
          <EmptyState title="No refunds" description="No refund has been raised against this transaction." />
        ) : (
          <ul className="flex flex-col gap-2 text-sm">
            {transaction.refunds.map((refund) => (
              <li key={refund.id} className="flex flex-wrap items-center justify-between gap-3 rounded-xl border border-line p-3">
                <div className="min-w-0 flex-1">
                  <p className="text-fg">{refund.reason}</p>
                  <p className="mt-0.5 text-xs text-fg-subtle">
                    {REFUND_TYPE_LABELS[refund.type]} · {REFUND_METHOD_LABELS[refund.method]}
                  </p>
                </div>
                <Badge tone={REFUND_STATUS_TONES[refund.status]}>{REFUND_STATUS_LABELS[refund.status]}</Badge>
                <span className="nums font-medium text-fg">{formatCurrency(refund.amount)}</span>
              </li>
            ))}
          </ul>
        )}
      </Card>
    </div>
  );
}
