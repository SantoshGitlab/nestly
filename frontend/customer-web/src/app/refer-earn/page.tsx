"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { RequireAuth } from "@/components/RequireAuth";
import { Alert, Button, Card, PageHeading } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import type { ReferralHistoryItemResponse, ReferralSummaryResponse } from "@/lib/types";

/** Readable label for a ReferralStatus enum member name (REFERRAL.md, task 169). */
function statusLabel(status: string): string {
  switch (status) {
    case "Registered":
      return "Signed up";
    case "Qualified":
      return "Qualified";
    case "Rewarded":
      return "Rewarded";
    case "FraudFlagged":
      return "Under review";
    case "FraudRejected":
      return "Rejected";
    default:
      return status;
  }
}

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
    <main className="mx-auto w-full max-w-3xl px-6 py-12">
      <PageHeading title="Refer & Earn" subtitle="Share Nestly with friends and earn a reward for every qualifying booking." />

      <div className="mb-8">
        {summaryQuery.isPending ? (
          <Card title="Your referral code">
            <p className="text-sm text-neutral-500">Loading…</p>
          </Card>
        ) : summaryQuery.isError ? (
          <Card title="Your referral code">
            <Alert>{describeError(summaryQuery.error)}</Alert>
          </Card>
        ) : (
          <ReferralSummaryCard summary={summaryQuery.data} />
        )}
      </div>

      <h2 className="mb-3 text-lg font-semibold">Referral history</h2>

      {historyQuery.isPending ? (
        <p className="text-sm text-neutral-500">Loading history…</p>
      ) : historyQuery.isError ? (
        <Alert>{describeError(historyQuery.error)}</Alert>
      ) : historyQuery.data.length === 0 ? (
        <Card title="No referrals yet">
          <p className="text-sm text-neutral-600 dark:text-neutral-400">
            Share your code above to see your invites show up here.
          </p>
        </Card>
      ) : (
        <ul className="flex flex-col gap-3">
          {historyQuery.data.map((item) => (
            <li key={item.id}>
              <Card title={item.refereeName}>
                <div className="flex items-center justify-between gap-3 text-sm">
                  <div className="flex flex-col gap-1 text-neutral-600 dark:text-neutral-400">
                    <span>Signed up {new Date(item.registeredAtUtc).toLocaleDateString()}</span>
                    {item.qualifiedAtUtc ? (
                      <span>Qualified {new Date(item.qualifiedAtUtc).toLocaleDateString()}</span>
                    ) : null}
                  </div>
                  <div className="flex flex-col items-end gap-1">
                    <span className="font-semibold">{statusLabel(item.status)}</span>
                    {item.rewardEarned ? (
                      <span className="text-xs text-green-700 dark:text-green-400">
                        +₹{item.rewardEarned.toFixed(2)}
                      </span>
                    ) : null}
                  </div>
                </div>
              </Card>
            </li>
          ))}
        </ul>
      )}
    </main>
  );
}

/** Code display + copy-to-clipboard + native share sheet - the two ways a customer actually shares a link on mobile vs. desktop. */
function ReferralSummaryCard({ summary }: { summary: ReferralSummaryResponse }) {
  const [copied, setCopied] = useState(false);

  async function handleCopy() {
    await navigator.clipboard.writeText(summary.shareLink);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }

  async function handleShare() {
    if (navigator.share) {
      await navigator.share({ title: "Join me on Nestly", url: summary.shareLink });
    } else {
      await handleCopy();
    }
  }

  return (
    <Card title="Your referral code">
      <div className="flex flex-col gap-4">
        <div className="flex items-center justify-between gap-3 rounded-lg border border-black/10 bg-neutral-50 px-4 py-3 dark:border-white/15 dark:bg-neutral-800">
          <span className="font-mono text-lg font-semibold tracking-wide">{summary.referralCode}</span>
          <div className="flex gap-2">
            <Button type="button" onClick={handleCopy}>
              {copied ? "Copied" : "Copy link"}
            </Button>
            <Button type="button" onClick={handleShare}>
              Share
            </Button>
          </div>
        </div>

        <div className="grid grid-cols-3 gap-3 text-center text-sm">
          <div>
            <div className="text-2xl font-semibold">{summary.invitedCount}</div>
            <div className="text-neutral-600 dark:text-neutral-400">Invited</div>
          </div>
          <div>
            <div className="text-2xl font-semibold">{summary.qualifiedCount}</div>
            <div className="text-neutral-600 dark:text-neutral-400">Qualified</div>
          </div>
          <div>
            <div className="text-2xl font-semibold">{summary.rewardedCount}</div>
            <div className="text-neutral-600 dark:text-neutral-400">Rewarded</div>
          </div>
        </div>

        <div className="text-center text-sm text-neutral-600 dark:text-neutral-400">
          Total earned: <span className="font-semibold text-black dark:text-white">₹{summary.totalEarned.toFixed(2)}</span>
        </div>
      </div>
    </Card>
  );
}
