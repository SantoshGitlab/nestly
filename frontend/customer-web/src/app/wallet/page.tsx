"use client";

import { useQuery } from "@tanstack/react-query";
import type { UseQueryResult } from "@tanstack/react-query";
import { BannerBreadcrumb, formatInstant, inr } from "@/components/patterns";
import { PageBanner } from "@/components/PageBanner";
import { RequireAuth } from "@/components/RequireAuth";
import {
  Alert,
  Badge,
  Button,
  Card,
  EmptyState,
  LinkButton,
  Skeleton,
  TBody,
  TD,
  TH,
  THead,
  TR,
  Table,
  cx,
} from "@/components/ui";
import type { BadgeTone } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import { WalletEntryType, WalletSourceType } from "@/lib/types";
import type { WalletBalanceResponse, WalletLedgerEntryResponse } from "@/lib/types";

/**
 * Wallet balance and ledger (tasks 78a-b, SRS 11.17).
 */
export default function WalletPage() {
  return (
    <RequireAuth>
      <WalletScreen />
    </RequireAuth>
  );
}

/**
 * Readable label for a ledger entry's source (SRS 11.17, GUIDELINES #4 of
 * docs/NESTLY-COINS.md - "a breakdown showing how much of the applied
 * balance was coins vs. other credit"). Real gap fixed here, not just
 * NestlyCoinsReward added alongside it (task 203): the three Referral
 * source types already existed on the backend but were never added to this
 * switch, silently falling through to the generic "Wallet" label.
 */
function sourceLabel(sourceType: WalletSourceType): string {
  switch (sourceType) {
    case WalletSourceType.Refund:
      return "Refund";
    case WalletSourceType.PromotionalCredit:
      return "Promotional credit";
    case WalletSourceType.ManualAdjustment:
      return "Adjustment";
    case WalletSourceType.ReferralReward:
      return "Referral reward";
    case WalletSourceType.ReferralMilestoneBonus:
      return "Referral milestone bonus";
    case WalletSourceType.ReferralCreditExpiry:
      return "Referral credit expired";
    case WalletSourceType.NestlyCoinsReward:
      return "Nestly Coins earned";
    case WalletSourceType.NestlyCoinsClawback:
      return "Nestly Coins clawed back";
    case WalletSourceType.BookingWalletCredit:
      return "Applied to booking";
    case WalletSourceType.BookingWalletCreditReversal:
      return "Wallet credit reversed - booking refunded";
    default:
      return "Wallet";
  }
}

/**
 * `accent` is the reward tone across the product, so the two Nestly Coins
 * sources carry it and nothing else does — that is the whole point of keeping
 * the ramp reserved. Everything else takes a neutral pill: the credit/debit
 * direction is already carried by the amount column, and colouring the source
 * as well would say the same thing twice in two different vocabularies.
 */
function sourceTone(sourceType: WalletSourceType): BadgeTone {
  switch (sourceType) {
    case WalletSourceType.NestlyCoinsReward:
    case WalletSourceType.NestlyCoinsClawback:
      return "accent";
    case WalletSourceType.ReferralReward:
    case WalletSourceType.ReferralMilestoneBonus:
      return "brand";
    default:
      return "neutral";
  }
}

function WalletScreen() {
  const balanceQuery = useQuery({
    queryKey: ["wallet-balance"],
    queryFn: () =>
      apiFetch<WalletBalanceResponse>(`${API_V1}/wallet/balance`, { authenticated: true }),
  });

  const ledgerQuery = useQuery({
    queryKey: ["wallet-ledger"],
    queryFn: () =>
      apiFetch<WalletLedgerEntryResponse[]>(`${API_V1}/wallet/ledger`, { authenticated: true }),
  });

  return (
    <main className="flex w-full flex-col">
      <PageBanner
        title="Wallet"
        description="Your Nestly wallet balance and transaction history."
        breadcrumb={<BannerBreadcrumb items={[{ label: "Home", href: "/" }, { label: "Wallet" }]} />}
      />

      <div className="mx-auto w-full max-w-7xl px-4 py-10 sm:px-6 sm:py-14">
        <div className="flex animate-rise flex-col gap-6">
          <BalanceCard query={balanceQuery} />
          <LedgerCard query={ledgerQuery} />
        </div>
      </div>
    </main>
  );
}

function BalanceCard({
  query,
}: {
  query: UseQueryResult<WalletBalanceResponse>;
}) {
  if (query.isPending) {
    return (
      <Card title="Balance">
        <div className="flex flex-col gap-2" aria-hidden>
          <Skeleton className="h-9 w-40" />
          <Skeleton className="h-3.5 w-64" />
        </div>
      </Card>
    );
  }

  if (query.isError) {
    return (
      <Card title="Balance">
        <Alert
          tone="error"
          title="Couldn't load your balance"
          action={
            <Button size="sm" variant="secondary" onClick={() => query.refetch()}>
              Retry
            </Button>
          }
        >
          {describeError(query.error)}
        </Alert>
      </Card>
    );
  }

  return (
    <Card title="Balance">
      {/* The figure is refetched in place, so it is announced rather than
          silently swapped under a customer who is reading it. */}
      <p className="nums text-display-sm font-semibold text-fg" aria-live="polite">
        {inr(query.data.balance)}
      </p>
      <p className="mt-1.5 text-sm leading-relaxed text-fg-muted">
        Use it at checkout: turn on &ldquo;Use my wallet balance&rdquo; on your booking summary to
        put it towards that booking, after any coupon or subscription discount.
      </p>
    </Card>
  );
}

function LedgerCard({
  query,
}: {
  query: UseQueryResult<WalletLedgerEntryResponse[]>;
}) {
  if (query.isPending) {
    return (
      <Card title="Transaction history">
        <div className="flex flex-col gap-4" aria-hidden>
          {[0, 1, 2, 3].map((row) => (
            <div key={row} className="flex items-center justify-between gap-4">
              <div className="flex min-w-0 flex-1 flex-col gap-2">
                <Skeleton className="h-3.5 w-40" />
                <Skeleton className="h-3 w-28" />
              </div>
              <Skeleton className="h-3.5 w-20" />
            </div>
          ))}
        </div>
      </Card>
    );
  }

  if (query.isError) {
    return (
      <Card title="Transaction history">
        <Alert
          tone="error"
          title="Couldn't load your transactions"
          action={
            <Button size="sm" variant="secondary" onClick={() => query.refetch()}>
              Retry
            </Button>
          }
        >
          {describeError(query.error)}
        </Alert>
      </Card>
    );
  }

  if (query.data.length === 0) {
    return (
      <EmptyState
        icon={<WalletIcon />}
        title="No wallet activity yet"
        description="Refunds, promotional credit and referral rewards all land here. Turn on &ldquo;Use my wallet balance&rdquo; on your next booking summary to put it towards that booking."
        action={<LinkButton href="/categories">Browse services</LinkButton>}
      />
    );
  }

  return (
    <Card title="Transaction history" flush>
      <Table>
        <caption className="sr-only">
          Wallet transactions, newest first, with the running balance after each one.
        </caption>
        <THead>
          <TR>
            <TH>Transaction</TH>
            <TH numeric>Amount</TH>
            <TH numeric>Balance after</TH>
          </TR>
        </THead>
        <TBody>
          {query.data.map((entry) => (
            <LedgerRow key={entry.id} entry={entry} />
          ))}
        </TBody>
      </Table>
    </Card>
  );
}

function LedgerRow({ entry }: { entry: WalletLedgerEntryResponse }) {
  const isCredit = entry.entryType === WalletEntryType.Credit;

  return (
    <TR>
      <TD>
        <div className="flex min-w-0 flex-col gap-1.5">
          <Badge tone={sourceTone(entry.sourceType)} className="self-start">
            {sourceLabel(entry.sourceType)}
          </Badge>
          <span className="text-sm text-fg">{entry.description}</span>
          <span className="nums text-xs text-fg-subtle">{formatInstant(entry.createdAtUtc)}</span>
        </div>
      </TD>
      <TD numeric>
        {/* Direction was previously carried by green/red alone, which is
            invisible to anyone who cannot separate the two. The sign is the
            primary signal, the colour reinforces it, and the word itself is
            there for a screen reader. */}
        <span className={cx("font-semibold", isCredit ? "text-success" : "text-danger")}>
          <span className="sr-only">{isCredit ? "Credit " : "Debit "}</span>
          {isCredit ? "+" : "−"}
          {inr(entry.amount)}
        </span>
      </TD>
      <TD numeric className="text-fg-muted">
        {inr(entry.balanceAfter)}
      </TD>
    </TR>
  );
}

function WalletIcon() {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.75"
      strokeLinecap="round"
      strokeLinejoin="round"
      className="h-5 w-5"
      aria-hidden
    >
      <path d="M3 7.5A2.5 2.5 0 0 1 5.5 5H18a2 2 0 0 1 2 2v1" />
      <path d="M3 7.5V17a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-2" />
      <path d="M21 11h-4a2 2 0 0 0 0 4h4z" />
    </svg>
  );
}
