"use client";

import { useQuery } from "@tanstack/react-query";
import type { UseQueryResult } from "@tanstack/react-query";
import { motion } from "motion/react";
import { useEffect, useRef, useState } from "react";
import { BannerBreadcrumb, formatInstantDate, inr } from "@/components/patterns";
import { Reveal, revealItem } from "@/components/motion";
import { PageBanner } from "@/components/PageBanner";
import { RequireAuth } from "@/components/RequireAuth";
import {
  Alert,
  Badge,
  Button,
  Card,
  EmptyState,
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
import { API_V1, apiFetch, describeError } from "@/lib/api";
import type { ReferralHistoryItemResponse, ReferralSummaryResponse } from "@/lib/types";

/**
 * Refer & Earn screen: the customer's own referral code/share link,
 * lifetime stats, and referral history (REFERRAL.md, task 169), calling
 * GET /referral and /referral/history (task 168).
 */
export default function ReferEarnPage() {
  return (
    <RequireAuth>
      <ReferEarnScreen />
    </RequireAuth>
  );
}

/** The status arrives as the ReferralStatus enum *member name*, not a number. */
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

/** `accent` is the product's reward tone, so only a paid-out referral wears it. */
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

function ReferEarnScreen() {
  const summaryQuery = useQuery({
    queryKey: ["referral-summary"],
    queryFn: () => apiFetch<ReferralSummaryResponse>(`${API_V1}/referral`, { authenticated: true }),
  });

  const historyQuery = useQuery({
    queryKey: ["referral-history"],
    queryFn: () =>
      apiFetch<ReferralHistoryItemResponse[]>(`${API_V1}/referral/history`, { authenticated: true }),
  });

  return (
    <main className="flex w-full flex-col">
      <PageBanner
        title="Refer & Earn"
        description="Share Nestly with friends and earn a reward for every qualifying booking."
        breadcrumb={<BannerBreadcrumb items={[{ label: "Home", href: "/" }, { label: "Refer & Earn" }]} />}
      />

      <div className="mx-auto w-full max-w-7xl px-4 py-10 sm:px-6 sm:py-14">
        <div className="flex animate-rise flex-col gap-6">
          <ShareCard query={summaryQuery} />
          <StatsRow query={summaryQuery} />
          <HistoryCard query={historyQuery} />
        </div>
      </div>
    </main>
  );
}

/* -------------------------------------------------------------------------- */
/* Code + share                                                               */
/* -------------------------------------------------------------------------- */

function ShareCard({ query }: { query: UseQueryResult<ReferralSummaryResponse> }) {
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
        <Alert
          tone="error"
          title="Couldn't load your referral code"
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

  return <ReferralShare summary={query.data} />;
}

/** What a share attempt did, so the outcome can be announced rather than mimed. */
type ShareFeedback = { tone: "success" | "error"; message: string } | null;

function ReferralShare({ summary }: { summary: ReferralSummaryResponse }) {
  const [feedback, setFeedback] = useState<ShareFeedback>(null);
  const timeoutRef = useRef<number | null>(null);

  // Clearing the confirmation on a timer means the timer can outlive the
  // screen — navigating away mid-countdown otherwise sets state on a component
  // that is already gone.
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

  /**
   * `navigator.clipboard` is undefined on an insecure origin and its write can
   * be refused outright by permissions policy. The unguarded `await` this
   * replaces produced an unhandled rejection and left the button silently
   * claiming nothing — so the failure path now says what to do instead.
   */
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
      await navigator.share({ title: "Join me on Nestly", url: summary.shareLink });
    } catch (error) {
      // Dismissing the OS share sheet rejects with AbortError. That is the
      // user declining, not a failure, and reporting it as one is worse than
      // saying nothing at all.
      if (error instanceof DOMException && error.name === "AbortError") return;
      await copy(summary.shareLink, "Invite link");
    }
  };

  return (
    <Card
      title="Your referral code"
      description="Your friend gets a welcome offer, and you earn once their first booking completes."
    >
      <div className="flex flex-col gap-4">
        <div className="rounded-xl border border-line bg-surface-2 px-4 py-3">
          <p className="text-xs font-medium uppercase tracking-wide text-fg-muted">Referral code</p>
          {/* Selectable rather than decorative: when the clipboard API is
              unavailable this is the manual fallback. */}
          <p className="nums mt-1 select-all font-mono text-xl font-semibold text-fg">
            {summary.referralCode}
          </p>
        </div>

        <div className="rounded-xl border border-line bg-surface-2 px-4 py-3">
          <p className="text-xs font-medium uppercase tracking-wide text-fg-muted">Invite link</p>
          <p className="mt-1 select-all break-all text-sm text-fg-muted">{summary.shareLink}</p>
        </div>

        <div className="flex flex-wrap gap-2">
          <Button type="button" onClick={share} icon={<ShareIcon />}>
            Share invite
          </Button>
          <Button
            type="button"
            variant="secondary"
            onClick={() => copy(summary.shareLink, "Invite link")}
          >
            Copy link
          </Button>
          <Button
            type="button"
            variant="secondary"
            onClick={() => copy(summary.referralCode, "Referral code")}
          >
            Copy code
          </Button>
        </div>

        {/* A live region rather than a label swap on the button: the old
            "Copied" text changed the button's own accessible name, which
            renames the control mid-interaction instead of reporting a result. */}
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

/* -------------------------------------------------------------------------- */
/* Lifetime stats                                                             */
/* -------------------------------------------------------------------------- */

function StatsRow({ query }: { query: UseQueryResult<ReferralSummaryResponse> }) {
  if (query.isPending) {
    return (
      <div className="grid grid-cols-2 gap-4 sm:grid-cols-4" aria-hidden>
        {[0, 1, 2, 3].map((tile) => (
          <Skeleton key={tile} className="h-28 rounded-2xl" />
        ))}
      </div>
    );
  }

  // The share card above already reports the failure and owns the retry;
  // repeating it here would be two alerts for one request.
  if (query.isError) return null;

  const summary = query.data;

  return (
    <Reveal className="grid grid-cols-2 gap-4 sm:grid-cols-4">
      <motion.div variants={revealItem}>
        <StatTile label="Invited" value={`${summary.invitedCount}`} />
      </motion.div>
      <motion.div variants={revealItem}>
        <StatTile label="Qualified" value={`${summary.qualifiedCount}`} />
      </motion.div>
      <motion.div variants={revealItem}>
        <StatTile label="Rewarded" value={`${summary.rewardedCount}`} />
      </motion.div>
      <motion.div variants={revealItem}>
        <StatTile label="Total earned" value={inr(summary.totalEarned)} />
      </motion.div>
    </Reveal>
  );
}

/* -------------------------------------------------------------------------- */
/* History                                                                    */
/* -------------------------------------------------------------------------- */

/** Shared by the card list and the table so the two can never describe themselves differently to a screen reader. */
const HISTORY_CAPTION = "Everyone who signed up with your code, and what each one has earned you.";

function HistoryCard({ query }: { query: UseQueryResult<ReferralHistoryItemResponse[]> }) {
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
        <Alert
          tone="error"
          title="Couldn't load your referrals"
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
        icon={<GiftIcon />}
        title="No referrals yet"
        description="Share your code above. Invites appear here as soon as a friend signs up with it, and turn into a reward once their first booking completes."
      />
    );
  }

  return (
    <Card title="Referral history" flush>
      {/*
       * Task 373, the other half of the finding task 365 closed for the
       * wallet ledger: three columns, one of them numeric, is what
       * docs/FRONTEND.md RESPONSIVE DESIGN asks to collapse rather than let
       * scroll sideways on a phone. Deliberately the same shape as
       * wallet/page.tsx rather than a second invention - each row renders
       * twice, exactly one visible at a time, CSS-only, breaking at `md`
       * (this app's own mobile/desktop split).
       */}
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
              <TH>Friend</TH>
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

/** Who signed up, and when. Identical in both layouts; only its surroundings differ. */
function HistoryIdentity({ item }: { item: ReferralHistoryItemResponse }) {
  return (
    <div className="flex min-w-0 flex-col gap-1">
      <span className="font-medium text-fg">{item.refereeName}</span>
      <span className="nums text-xs text-fg-subtle">
        Signed up {formatInstantDate(item.registeredAtUtc)}
        {item.qualifiedAtUtc ? ` · Qualified ${formatInstantDate(item.qualifiedAtUtc)}` : ""}
      </span>
    </div>
  );
}

/** Shared so the two layouts can never disagree about what a referral earned. */
function HistoryReward({ item }: { item: ReferralHistoryItemResponse }) {
  // `!= null` rather than truthiness: a genuine ₹0.00 reward is a real,
  // recorded outcome and should not render as a dash.
  return item.rewardEarned != null ? (
    <span className="font-semibold text-success">+{inr(item.rewardEarned)}</span>
  ) : (
    <span className="text-fg-subtle" aria-label="No reward yet">
      —
    </span>
  );
}

/**
 * The below-`md` card. Same arrangement as the wallet ledger's: the status
 * badge leads, the amount holds the right of the entry's own line, and
 * nothing carries a visible column label - with only three fields, and one of
 * them a badge that names itself, a label:value stack would add words without
 * adding meaning.
 */
function HistoryCardRow({ item }: { item: ReferralHistoryItemResponse }) {
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

function ShareIcon() {
  return (
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
      <circle cx="18" cy="5" r="3" />
      <circle cx="6" cy="12" r="3" />
      <circle cx="18" cy="19" r="3" />
      <path d="m8.6 13.5 6.8 4M15.4 6.5l-6.8 4" />
    </svg>
  );
}

function GiftIcon() {
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
      <path d="M20 12v8a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1v-8" />
      <path d="M2 8h20v4H2zM12 8v13" />
      <path d="M12 8H7.5a2.5 2.5 0 0 1 0-5C11 3 12 8 12 8ZM12 8h4.5a2.5 2.5 0 0 0 0-5C13 3 12 8 12 8Z" />
    </svg>
  );
}
