"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useState } from "react";
import { RequireAdminAuth } from "@/components/RequireAdminAuth";
import { Alert, Card, Field, PageHeading, StatTile } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";

interface ReferralFunnelReportResponse {
  invitedCount: number;
  registeredCount: number;
  qualifiedCount: number;
  rewardedCount: number;
}

interface ReferralCostReportResponse {
  totalWalletCreditCost: number;
  totalCouponCost: number;
  totalCost: number;
  rewardedReferralCount: number;
  milestoneBonusCount: number;
}

/**
 * Admin funnel + total program cost reports (task 171). GET
 * /admin/referral/reports/funnel and /reports/cost, both accepting an
 * optional [fromUtc, toUtc] date range.
 */
export default function ReferralReportsPage() {
  return (
    <RequireAdminAuth>
      <ReferralReportsScreen />
    </RequireAdminAuth>
  );
}

function ReferralReportsScreen() {
  const [fromDate, setFromDate] = useState("");
  const [toDate, setToDate] = useState("");

  const query = `${fromDate ? `fromUtc=${fromDate}` : ""}${fromDate && toDate ? "&" : ""}${toDate ? `toUtc=${toDate}` : ""}`;
  const suffix = query ? `?${query}` : "";

  const funnelQuery = useQuery({
    queryKey: ["referral-funnel-report", fromDate, toDate],
    queryFn: () =>
      apiFetch<ReferralFunnelReportResponse>(`${API_V1}/referral/reports/funnel${suffix}`, { authenticated: true }),
  });

  const costQuery = useQuery({
    queryKey: ["referral-cost-report", fromDate, toDate],
    queryFn: () =>
      apiFetch<ReferralCostReportResponse>(`${API_V1}/referral/reports/cost${suffix}`, { authenticated: true }),
  });

  return (
    <main className="flex flex-col gap-6">
      <PageHeading title="Referral Reports" subtitle="Funnel and total program cost." />
      <div>
        <Link href="/referral" className="text-sm font-medium hover:underline">
          ← Referrals & fraud queue
        </Link>
        {" · "}
        <Link href="/referral/config" className="text-sm font-medium hover:underline">
          Program config
        </Link>
      </div>

      <Card title="Date range (optional)">
        <div className="flex items-end gap-3">
          <Field label="From" type="date" value={fromDate} onChange={(e) => setFromDate(e.target.value)} />
          <Field label="To" type="date" value={toDate} onChange={(e) => setToDate(e.target.value)} />
        </div>
      </Card>

      <section>
        <h2 className="mb-3 text-lg font-semibold">Funnel</h2>
        {funnelQuery.isPending ? (
          <p className="text-sm text-neutral-500">Loading…</p>
        ) : funnelQuery.isError ? (
          <Alert>{describeError(funnelQuery.error)}</Alert>
        ) : (
          <div className="grid grid-cols-4 gap-4">
            <StatTile label="Invited" value={String(funnelQuery.data.invitedCount)} />
            <StatTile label="Registered" value={String(funnelQuery.data.registeredCount)} />
            <StatTile label="Qualified" value={String(funnelQuery.data.qualifiedCount)} />
            <StatTile label="Rewarded" value={String(funnelQuery.data.rewardedCount)} />
          </div>
        )}
      </section>

      <section>
        <h2 className="mb-3 text-lg font-semibold">Program cost</h2>
        {costQuery.isPending ? (
          <p className="text-sm text-neutral-500">Loading…</p>
        ) : costQuery.isError ? (
          <Alert>{describeError(costQuery.error)}</Alert>
        ) : (
          <div className="grid grid-cols-3 gap-4">
            <StatTile label="Wallet credit cost" value={`₹${costQuery.data.totalWalletCreditCost.toFixed(2)}`} />
            <StatTile label="Coupon cost" value={`₹${costQuery.data.totalCouponCost.toFixed(2)}`} />
            <StatTile label="Total cost" value={`₹${costQuery.data.totalCost.toFixed(2)}`} />
          </div>
        )}
      </section>
    </main>
  );
}
