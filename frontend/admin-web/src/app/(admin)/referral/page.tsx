"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useEffect, useState } from "react";
import { RequireAdminAuth } from "@/components/RequireAdminAuth";
import { Alert, Button, Card, PageHeading } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import { getSessionClaims, subscribeToAuthChanges } from "@/lib/auth";
import { canWriteModule } from "@/lib/permissions";
import type { AdminSessionClaims } from "@/lib/types";

interface ReferralAdminListItemResponse {
  id: string;
  referrerName: string;
  refereeName: string;
  status: string;
  isFraudFlagged: boolean;
  registeredAtUtc: string;
  rewardedAtUtc: string | null;
}

interface ReferralAdminSearchResponse {
  items: ReferralAdminListItemResponse[];
  totalCount: number;
  page: number;
  pageSize: number;
}

interface ReferralAdminDetailResponse extends ReferralAdminListItemResponse {
  referrerMobile: string;
  refereeMobile: string;
  referralCodeUsed: string;
  referrerRewardValue: number;
  refereeRewardValue: number;
  qualifiedAtUtc: string | null;
  expiresAtUtc: string;
  fraudReviewNote: string | null;
}

/**
 * Admin referral list/detail + fraud review queue (task 170). GET
 * /admin/referral (search), GET /admin/referral/fraud-queue, GET
 * /admin/referral/{id}, POST .../flag|approve|reject.
 */
export default function ReferralAdminPage() {
  return (
    <RequireAdminAuth>
      <ReferralAdminScreen />
    </RequireAdminAuth>
  );
}

function ReferralAdminScreen() {
  const [claims, setClaims] = useState<AdminSessionClaims | null>(null);
  useEffect(() => {
    const sync = () => setClaims(getSessionClaims());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);
  const canWrite = canWriteModule(claims, "referral");

  const [tab, setTab] = useState<"all" | "fraud-queue">("all");
  const [selectedId, setSelectedId] = useState<string | null>(null);

  const searchQuery = useQuery({
    queryKey: ["referral-admin-list", tab],
    queryFn: () =>
      apiFetch<ReferralAdminSearchResponse>(
        `${API_V1}/referral${tab === "fraud-queue" ? "/fraud-queue" : ""}?pageSize=50`,
        { authenticated: true },
      ),
  });

  return (
    <main className="flex flex-col gap-6">
      <PageHeading title="Referrals" subtitle="Referral list, detail, and fraud review queue." />
      <div>
        <Link href="/referral/config" className="text-sm font-medium hover:underline">
          Program config
        </Link>
        {" · "}
        <Link href="/referral/reports" className="text-sm font-medium hover:underline">
          Reports
        </Link>
      </div>

      <div className="flex gap-2 border-b border-black/10 dark:border-white/10">
        <TabButton active={tab === "all"} onClick={() => setTab("all")}>
          All referrals
        </TabButton>
        <TabButton active={tab === "fraud-queue"} onClick={() => setTab("fraud-queue")}>
          Fraud queue
        </TabButton>
      </div>

      {searchQuery.isPending ? (
        <p className="text-sm text-neutral-500">Loading…</p>
      ) : searchQuery.isError ? (
        <Alert>{describeError(searchQuery.error)}</Alert>
      ) : searchQuery.data.items.length === 0 ? (
        <Card title="Nothing here">
          <p className="text-sm text-neutral-600 dark:text-neutral-400">
            {tab === "fraud-queue" ? "No referrals currently flagged for review." : "No referrals yet."}
          </p>
        </Card>
      ) : (
        <table className="w-full text-left text-sm">
          <thead className="text-neutral-600 dark:text-neutral-400">
            <tr>
              <th className="pb-2">Referrer</th>
              <th className="pb-2">Referee</th>
              <th className="pb-2">Status</th>
              <th className="pb-2">Flagged</th>
              <th className="pb-2">Registered</th>
              <th className="pb-2" />
            </tr>
          </thead>
          <tbody>
            {searchQuery.data.items.map((item) => (
              <tr key={item.id} className="border-t border-black/10 dark:border-white/10">
                <td className="py-2">{item.referrerName}</td>
                <td className="py-2">{item.refereeName}</td>
                <td className="py-2">{item.status}</td>
                <td className="py-2">{item.isFraudFlagged ? "Yes" : ""}</td>
                <td className="py-2">{new Date(item.registeredAtUtc).toLocaleDateString()}</td>
                <td className="py-2">
                  <button type="button" className="text-sm font-medium hover:underline" onClick={() => setSelectedId(item.id)}>
                    View
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {selectedId ? (
        <DetailPanel id={selectedId} canWrite={canWrite} onClose={() => setSelectedId(null)} />
      ) : null}
    </main>
  );
}

function TabButton({ active, onClick, children }: { active: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button
      type="button"
      onClick={onClick}
      className={`-mb-px border-b-2 px-3 py-2 text-sm font-medium ${
        active ? "border-black dark:border-white" : "border-transparent text-neutral-500 hover:text-black dark:hover:text-white"
      }`}
    >
      {children}
    </button>
  );
}

function DetailPanel({ id, canWrite, onClose }: { id: string; canWrite: boolean; onClose: () => void }) {
  const queryClient = useQueryClient();
  const [note, setNote] = useState("");

  const detailQuery = useQuery({
    queryKey: ["referral-admin-detail", id],
    queryFn: () => apiFetch<ReferralAdminDetailResponse>(`${API_V1}/referral/${id}`, { authenticated: true }),
  });

  const invalidate = () => {
    queryClient.invalidateQueries({ queryKey: ["referral-admin-detail", id] });
    queryClient.invalidateQueries({ queryKey: ["referral-admin-list"] });
  };

  const flagMutation = useMutation({
    mutationFn: () =>
      apiFetch(`${API_V1}/referral/${id}/flag`, { method: "POST", authenticated: true, body: JSON.stringify({ note: note || null }) }),
    onSuccess: invalidate,
  });
  const approveMutation = useMutation({
    mutationFn: () =>
      apiFetch(`${API_V1}/referral/${id}/approve`, { method: "POST", authenticated: true, body: JSON.stringify({ note: note || null }) }),
    onSuccess: invalidate,
  });
  const rejectMutation = useMutation({
    mutationFn: () =>
      apiFetch(`${API_V1}/referral/${id}/reject`, { method: "POST", authenticated: true, body: JSON.stringify({ note: note || null }) }),
    onSuccess: invalidate,
  });

  const anyPending = flagMutation.isPending || approveMutation.isPending || rejectMutation.isPending;
  const anyError = flagMutation.error ?? approveMutation.error ?? rejectMutation.error;

  return (
    <Card title="Referral detail">
      <button type="button" onClick={onClose} className="mb-3 text-sm font-medium hover:underline">
        Close
      </button>

      {detailQuery.isPending ? (
        <p className="text-sm text-neutral-500">Loading…</p>
      ) : detailQuery.isError ? (
        <Alert>{describeError(detailQuery.error)}</Alert>
      ) : (
        <div className="flex flex-col gap-4">
          {anyError ? <Alert>{describeError(anyError)}</Alert> : null}

          <dl className="grid grid-cols-2 gap-3 text-sm">
            <div>
              <dt className="text-neutral-600 dark:text-neutral-400">Referrer</dt>
              <dd>{detailQuery.data.referrerName} ({detailQuery.data.referrerMobile})</dd>
            </div>
            <div>
              <dt className="text-neutral-600 dark:text-neutral-400">Referee</dt>
              <dd>{detailQuery.data.refereeName} ({detailQuery.data.refereeMobile})</dd>
            </div>
            <div>
              <dt className="text-neutral-600 dark:text-neutral-400">Code used</dt>
              <dd>{detailQuery.data.referralCodeUsed}</dd>
            </div>
            <div>
              <dt className="text-neutral-600 dark:text-neutral-400">Status</dt>
              <dd>{detailQuery.data.status}</dd>
            </div>
            <div>
              <dt className="text-neutral-600 dark:text-neutral-400">Registered</dt>
              <dd>{new Date(detailQuery.data.registeredAtUtc).toLocaleString()}</dd>
            </div>
            <div>
              <dt className="text-neutral-600 dark:text-neutral-400">Expires</dt>
              <dd>{new Date(detailQuery.data.expiresAtUtc).toLocaleString()}</dd>
            </div>
            {detailQuery.data.fraudReviewNote ? (
              <div className="col-span-2">
                <dt className="text-neutral-600 dark:text-neutral-400">Fraud review note</dt>
                <dd>{detailQuery.data.fraudReviewNote}</dd>
              </div>
            ) : null}
          </dl>

          {canWrite ? (
            <div className="flex flex-col gap-2 border-t border-black/10 pt-4 dark:border-white/10">
              <input
                type="text"
                placeholder="Optional note"
                value={note}
                onChange={(e) => setNote(e.target.value)}
                className="rounded-lg border border-black/15 bg-transparent px-3 py-2 text-sm outline-none focus:border-black focus:ring-1 focus:ring-black dark:border-white/20 dark:focus:border-white dark:focus:ring-white"
              />
              <div className="flex gap-2">
                {!detailQuery.data.isFraudFlagged ? (
                  <Button type="button" variant="secondary" disabled={anyPending} onClick={() => flagMutation.mutate()}>
                    Flag for review
                  </Button>
                ) : (
                  <>
                    <Button type="button" disabled={anyPending} onClick={() => approveMutation.mutate()}>
                      Confirm fraud
                    </Button>
                    <Button type="button" variant="secondary" disabled={anyPending} onClick={() => rejectMutation.mutate()}>
                      Reject flag (false positive)
                    </Button>
                  </>
                )}
              </div>
            </div>
          ) : null}
        </div>
      )}
    </Card>
  );
}
