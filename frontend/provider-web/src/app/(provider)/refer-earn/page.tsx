"use client";

import { useQuery } from "@tanstack/react-query";
import type { UseQueryResult } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { ErrorState } from "@/components/states";
import {
  Badge,
  Button,
  Card,
  EmptyState,
  PageHeading,
  Skeleton,
  StatTile,
  TBody,
  TD,
  TH,
  THead,
  TR,
  Table,
} from "@/components/ui";
import type { BadgeTone } from "@/components/ui";
import { formatDate, formatInr } from "@/lib/format";
import { getReferralHistory, getReferralSummary } from "@/lib/referral-api";
import type { ProviderReferralHistoryItem, ProviderReferralSummary } from "@/lib/referral-types";

/**
 * Refer & Earn: a provider's own referral code/share link, lifetime stats,
 * and referral history (PROVIDER-REFERRAL.md), calling GET /referral and
 * /referral/history - mirrors customer-web's own refer-earn page, adapted to
 * provider-web's leaner component set and earning-ledger reward.
 */
export default function ReferEarnPage() {
  const summaryQuery = useQuery({ queryKey: ["provider-referral-summary"], queryFn: getReferralSummary });
  const historyQuery = useQuery({ queryKey: ["provider-referral-history"], queryFn: getReferralHistory });

  return (
    <div className="flex w-full max-w-4xl animate-rise flex-col gap-6">
      <PageHeading
        title="Refer & Earn"
        subtitle="Invite another provider to Nestly and earn once they complete their first few jobs."
      />
      <ShareCard query={summaryQuery} />
      <StatsRow query={summaryQuery} />
      <HistoryCard query={historyQuery} />
    </div>
  );
}

type ShareFeedback = { tone: "success" | "error"; message: string } | null;

function ShareCard({ query }: { query: UseQueryResult<ProviderReferralSummary> }) {
  if (query.isPending) {
    return (
      <Card title="Your referral code">
        <div className="flex flex-col gap-4" aria-hidden>
          <Skeleton className="h-14 rounded-xl" />
          <Skeleton className="h-10 w-full rounded-lg sm:w-64" />
        </div>
      </Card>
    );
  }

  if (query.isError) {
    return (
      <Card title="Your referral code">
        <ErrorState
          title="Couldn't load your referral code"
          error={query.error}
          onRetry={() => query.refetch()}
          isRetrying={query.isRefetching}
        />
      </Card>
    );
  }

  return <ReferralShare summary={query.data} />;
}

function ReferralShare({ summary }: { summary: ProviderReferralSummary }) {
  const [feedback, setFeedback] = useState<ShareFeedback>(null);
  const timeoutRef = useRef<number | null>(null);

  useEffect(
    () => () => {
      if (timeoutRef.current !== null) window.clearTimeout(timeoutRef.current);
    },
    [],
  );

  const announce = (next: ShareFeedback) => {
    if (timeoutRef.current !== null) window.clearTimeout(timeoutRef.current);
    setFeedback(next);
    timeoutRef.current = window.setTimeout(() => setFeedback(null), 4000);
  };

  const copy = async (value: string, label: string) => {
    try {
      if (!navigator.clipboard) throw new Error("Clipboard unavailable");
      await navigator.clipboard.writeText(value);
      announce({ tone: "success", message: `${label} copied.` });
    } catch {
      announce({
        tone: "error",
        message: `Couldn't copy automatically — select the ${label.toLowerCase()} above and copy it.`,
      });
    }
  };

  const share = async () => {
    if (!navigator.share) {
      await copy(summary.shareLink, "Invite link");
      return;
    }

    try {
      await navigator.share({ title: "Join Nestly as a provider", url: summary.shareLink });
    } catch (error) {
      if (error instanceof DOMException && error.name === "AbortError") return;
      await copy(summary.shareLink, "Invite link");
    }
  };

  return (
    <Card
      title="Your referral code"
      description="The provider you refer gets a welcome bonus, and you earn once they complete their first few jobs."
    >
      <div className="flex flex-col gap-4">
        <div className="rounded-xl border border-line bg-surface-2 px-4 py-3">
          <p className="text-xs font-medium uppercase tracking-wide text-fg-muted">Referral code</p>
          <p className="nums mt-1 select-all font-mono text-xl font-semibold text-fg">
            {summary.referralCode}
          </p>
        </div>

        <div className="rounded-xl border border-line bg-surface-2 px-4 py-3">
          <p className="text-xs font-medium uppercase tracking-wide text-fg-muted">Invite link</p>
          <p className="mt-1 select-all break-all text-sm text-fg-muted">{summary.shareLink}</p>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button type="button" onClick={share}>
            Share invite
          </Button>
          <Button type="button" variant="secondary" onClick={() => copy(summary.shareLink, "Invite link")}>
            Copy link
          </Button>
          <Button type="button" variant="secondary" onClick={() => copy(summary.referralCode, "Referral code")}>
            Copy code
          </Button>
        </div>

        <p
          role="status"
          aria-live="polite"
          className={feedback?.tone === "error" ? "text-sm text-danger" : "text-sm text-success"}
        >
          {feedback?.message ?? ""}
        </p>
      </div>
    </Card>
  );
}

function StatsRow({ query }: { query: UseQueryResult<ProviderReferralSummary> }) {
  if (query.isPending) {
    return (
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-4" aria-hidden>
        {[0, 1, 2, 3].map((tile) => (
          <Skeleton key={tile} className="h-28 rounded-2xl" />
        ))}
      </div>
    );
  }

  // The share card above already reports the failure and owns the retry.
  if (query.isError) return null;

  const summary = query.data;

  return (
    <div className="grid grid-cols-2 gap-4 sm:grid-cols-4">
      <StatTile tone="brand" label="Invited" value={`${summary.invitedCount}`} />
      <StatTile tone="warning" label="Qualified" value={`${summary.qualifiedCount}`} />
      <StatTile tone="success" label="Rewarded" value={`${summary.rewardedCount}`} />
      <StatTile tone="accent" label="Total earned" value={formatInr(summary.totalEarned)} />
    </div>
  );
}

const HISTORY_CAPTION = "Every provider who signed up with your code, and what each one has earned you.";

function statusLabel(status: string): string {
  switch (status) {
    case "Registered":
      return "Signed up";
    case "Qualified":
      return "Qualified";
    case "Rewarded":
      return "Rewarded";
    case "Expired":
      return "Expired";
    default:
      return status;
  }
}

function statusTone(status: string): BadgeTone {
  switch (status) {
    case "Rewarded":
      return "accent";
    case "Qualified":
      return "success";
    case "Registered":
      return "info";
    case "Expired":
      return "neutral";
    default:
      return "neutral";
  }
}

function HistoryCard({ query }: { query: UseQueryResult<ProviderReferralHistoryItem[]> }) {
  if (query.isPending) {
    return (
      <Card title="Referral history">
        <div className="flex flex-col gap-4" aria-hidden>
          {[0, 1, 2].map((row) => (
            <div key={row} className="flex items-center justify-between gap-4">
              <div className="flex min-w-0 flex-1 flex-col gap-2">
                <Skeleton className="h-3.5 w-36" />
                <Skeleton className="h-3 w-24" />
              </div>
              <Skeleton className="h-5 w-20 rounded-full" />
            </div>
          ))}
        </div>
      </Card>
    );
  }

  if (query.isError) {
    return (
      <Card title="Referral history">
        <ErrorState
          title="Couldn't load your referrals"
          error={query.error}
          onRetry={() => query.refetch()}
          isRetrying={query.isRefetching}
        />
      </Card>
    );
  }

  if (query.data.length === 0) {
    return (
      <EmptyState
        title="No referrals yet"
        description="Share your code above. Invites appear here as soon as another provider signs up with it, and turn into a reward once they complete their first few jobs."
      />
    );
  }

  return (
    <Card title="Referral history" flush>
      {/* Same mobile/desktop split as the earnings ledger: a card list below
          md, a table at md and up - one of the three columns is numeric, so
          this collapses rather than scrolling sideways on a phone. */}
      <ul aria-label={HISTORY_CAPTION} className="divide-y divide-line md:hidden">
        {query.data.map((item) => (
          <HistoryCardRow key={item.id} item={item} />
        ))}
      </ul>

      <div className="hidden md:block">
        <Table>
          <caption className="sr-only">{HISTORY_CAPTION}</caption>
          <THead>
            <TR>
              <TH>Provider</TH>
              <TH>Status</TH>
              <TH numeric>Reward</TH>
            </TR>
          </THead>
          <TBody>
            {query.data.map((item) => (
              <TR key={item.id}>
                <TD>
                  <HistoryIdentity item={item} />
                </TD>
                <TD>
                  <Badge tone={statusTone(item.status)}>{statusLabel(item.status)}</Badge>
                </TD>
                <TD numeric>
                  <HistoryReward item={item} />
                </TD>
              </TR>
            ))}
          </TBody>
        </Table>
      </div>
    </Card>
  );
}

function HistoryIdentity({ item }: { item: ProviderReferralHistoryItem }) {
  return (
    <div className="flex min-w-0 flex-col gap-1">
      <span className="font-medium text-fg">{item.refereeDisplayName}</span>
      <span className="nums text-xs text-fg-subtle">
        Signed up {formatDate(item.registeredAtUtc)}
        {item.qualifiedAtUtc ? ` · Qualified ${formatDate(item.qualifiedAtUtc)}` : ""}
      </span>
    </div>
  );
}

function HistoryReward({ item }: { item: ProviderReferralHistoryItem }) {
  return item.rewardEarned != null ? (
    <span className="font-semibold text-success">+{formatInr(item.rewardEarned)}</span>
  ) : (
    <span className="text-fg-subtle" aria-label="No reward yet">
      —
    </span>
  );
}

function HistoryCardRow({ item }: { item: ProviderReferralHistoryItem }) {
  return (
    <li className="flex items-start justify-between gap-3 px-4 py-3.5">
      <div className="flex min-w-0 flex-col items-start gap-1.5">
        <Badge tone={statusTone(item.status)}>{statusLabel(item.status)}</Badge>
        <HistoryIdentity item={item} />
      </div>
      <span className="shrink-0 text-right text-sm">
        <HistoryReward item={item} />
      </span>
    </li>
  );
}
