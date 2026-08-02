"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useEffect, useState } from "react";
import { RequireAdminAuth } from "@/components/RequireAdminAuth";
import { Alert, Button, Card, Field, PageHeading, Select } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import { getSessionClaims, subscribeToAuthChanges } from "@/lib/auth";
import { canWriteModule } from "@/lib/permissions";
import type { AdminSessionClaims } from "@/lib/types";

const REWARD_TYPE_OPTIONS = [
  { value: "0", label: "Wallet credit" },
  { value: "1", label: "Coupon" },
] as const;

enum ReferralRewardType {
  WalletCredit = 0,
  Coupon = 1,
}

interface ReferralProgramConfigResponse {
  id: string;
  referrerRewardType: ReferralRewardType;
  referrerRewardValue: number;
  refereeRewardType: ReferralRewardType;
  refereeRewardValue: number;
  minQualifyingOrderAmount: number;
  referralExpiryDays: number;
  maxReferralsPerCustomer: number | null;
  isActive: boolean;
  updatedAtUtc: string;
}

interface ReferralMilestoneResponse {
  id: string;
  thresholdCount: number;
  bonusType: ReferralRewardType;
  bonusValue: number;
  isActive: boolean;
  createdAtUtc: string;
}

/**
 * Admin CRUD for the referral program config + milestone tiers (task 167).
 * GET/PUT /admin/referral/config, GET/POST/activate/deactivate
 * /admin/referral/config/milestones.
 */
export default function ReferralConfigPage() {
  return (
    <RequireAdminAuth>
      <ReferralConfigScreen />
    </RequireAdminAuth>
  );
}

function ReferralConfigScreen() {
  const [claims, setClaims] = useState<AdminSessionClaims | null>(null);
  useEffect(() => {
    const sync = () => setClaims(getSessionClaims());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);
  const canWrite = canWriteModule(claims, "referral");

  const configQuery = useQuery({
    queryKey: ["referral-config"],
    queryFn: () => apiFetch<ReferralProgramConfigResponse>(`${API_V1}/referral/config`, { authenticated: true }),
  });

  const milestonesQuery = useQuery({
    queryKey: ["referral-milestones"],
    queryFn: () =>
      apiFetch<ReferralMilestoneResponse[]>(`${API_V1}/referral/config/milestones`, { authenticated: true }),
  });

  return (
    <main className="flex flex-col gap-8">
      <PageHeading title="Referral Program Config" subtitle="Reward values, qualifying amount, expiry, and milestone tiers." />
      <div>
        <Link href="/referral" className="text-sm font-medium hover:underline">
          ← Referrals & fraud queue
        </Link>
        {" · "}
        <Link href="/referral/reports" className="text-sm font-medium hover:underline">
          Reports
        </Link>
      </div>

      {configQuery.isPending ? (
        <p className="text-sm text-neutral-500">Loading…</p>
      ) : configQuery.isError ? (
        <Alert>{describeError(configQuery.error)}</Alert>
      ) : (
        <ConfigForm config={configQuery.data} canWrite={canWrite} />
      )}

      <Card title="Milestone tiers">
        {milestonesQuery.isPending ? (
          <p className="text-sm text-neutral-500">Loading…</p>
        ) : milestonesQuery.isError ? (
          <Alert>{describeError(milestonesQuery.error)}</Alert>
        ) : (
          <MilestonesPanel milestones={milestonesQuery.data} canWrite={canWrite} />
        )}
      </Card>
    </main>
  );
}

function ConfigForm({ config, canWrite }: { config: ReferralProgramConfigResponse; canWrite: boolean }) {
  const queryClient = useQueryClient();
  const [form, setForm] = useState(config);

  useEffect(() => setForm(config), [config]);

  const updateMutation = useMutation({
    mutationFn: () =>
      apiFetch<ReferralProgramConfigResponse>(`${API_V1}/referral/config`, {
        method: "PUT",
        authenticated: true,
        body: JSON.stringify({
          referrerRewardType: form.referrerRewardType,
          referrerRewardValue: form.referrerRewardValue,
          refereeRewardType: form.refereeRewardType,
          refereeRewardValue: form.refereeRewardValue,
          minQualifyingOrderAmount: form.minQualifyingOrderAmount,
          referralExpiryDays: form.referralExpiryDays,
          maxReferralsPerCustomer: form.maxReferralsPerCustomer,
          isActive: form.isActive,
        }),
      }),
    onSuccess: (updated) => {
      setForm(updated);
      queryClient.setQueryData(["referral-config"], updated);
    },
  });

  return (
    <Card title="Program config">
      <form
        className="flex flex-col gap-4"
        onSubmit={(event) => {
          event.preventDefault();
          updateMutation.mutate();
        }}
      >
        {updateMutation.isError ? <Alert>{describeError(updateMutation.error)}</Alert> : null}
        {updateMutation.isSuccess ? <Alert tone="success">Saved.</Alert> : null}

        <div className="grid grid-cols-2 gap-4">
          <Select
            label="Referrer reward type"
            options={REWARD_TYPE_OPTIONS}
            value={String(form.referrerRewardType)}
            disabled={!canWrite}
            onChange={(e) => setForm({ ...form, referrerRewardType: Number(e.target.value) })}
          />
          <Field
            label="Referrer reward value"
            type="number"
            step="0.01"
            disabled={!canWrite}
            value={form.referrerRewardValue}
            onChange={(e) => setForm({ ...form, referrerRewardValue: Number(e.target.value) })}
          />
          <Select
            label="Referee reward type"
            options={REWARD_TYPE_OPTIONS}
            value={String(form.refereeRewardType)}
            disabled={!canWrite}
            onChange={(e) => setForm({ ...form, refereeRewardType: Number(e.target.value) })}
          />
          <Field
            label="Referee reward value"
            type="number"
            step="0.01"
            disabled={!canWrite}
            value={form.refereeRewardValue}
            onChange={(e) => setForm({ ...form, refereeRewardValue: Number(e.target.value) })}
          />
          <Field
            label="Min qualifying order amount"
            type="number"
            step="0.01"
            disabled={!canWrite}
            value={form.minQualifyingOrderAmount}
            onChange={(e) => setForm({ ...form, minQualifyingOrderAmount: Number(e.target.value) })}
          />
          <Field
            label="Referral expiry (days)"
            type="number"
            disabled={!canWrite}
            value={form.referralExpiryDays}
            onChange={(e) => setForm({ ...form, referralExpiryDays: Number(e.target.value) })}
          />
          <Field
            label="Max referrals per customer (blank = unlimited)"
            type="number"
            disabled={!canWrite}
            value={form.maxReferralsPerCustomer ?? ""}
            onChange={(e) =>
              setForm({ ...form, maxReferralsPerCustomer: e.target.value === "" ? null : Number(e.target.value) })
            }
          />
        </div>

        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            disabled={!canWrite}
            checked={form.isActive}
            onChange={(e) => setForm({ ...form, isActive: e.target.checked })}
          />
          Program active
        </label>

        {canWrite ? (
          <Button type="submit" disabled={updateMutation.isPending} className="w-fit">
            {updateMutation.isPending ? "Saving…" : "Save"}
          </Button>
        ) : null}
      </form>
    </Card>
  );
}

function MilestonesPanel({
  milestones,
  canWrite,
}: {
  milestones: ReferralMilestoneResponse[];
  canWrite: boolean;
}) {
  const queryClient = useQueryClient();
  const [threshold, setThreshold] = useState("");
  const [bonusType, setBonusType] = useState(ReferralRewardType.WalletCredit);
  const [bonusValue, setBonusValue] = useState("");

  const createMutation = useMutation({
    mutationFn: () =>
      apiFetch<ReferralMilestoneResponse>(`${API_V1}/referral/config/milestones`, {
        method: "POST",
        authenticated: true,
        body: JSON.stringify({
          thresholdCount: Number(threshold),
          bonusType,
          bonusValue: Number(bonusValue),
        }),
      }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["referral-milestones"] });
      setThreshold("");
      setBonusValue("");
    },
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, activate }: { id: string; activate: boolean }) =>
      apiFetch<ReferralMilestoneResponse>(
        `${API_V1}/referral/config/milestones/${id}/${activate ? "activate" : "deactivate"}`,
        { method: "POST", authenticated: true },
      ),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["referral-milestones"] }),
  });

  return (
    <div className="flex flex-col gap-4">
      {createMutation.isError ? <Alert>{describeError(createMutation.error)}</Alert> : null}

      {milestones.length === 0 ? (
        <p className="text-sm text-neutral-600 dark:text-neutral-400">No milestone tiers configured yet.</p>
      ) : (
        <table className="w-full text-left text-sm">
          <thead className="text-neutral-600 dark:text-neutral-400">
            <tr>
              <th className="pb-2">Threshold (referrals)</th>
              <th className="pb-2">Bonus</th>
              <th className="pb-2">Status</th>
              {canWrite ? <th className="pb-2">Action</th> : null}
            </tr>
          </thead>
          <tbody>
            {milestones.map((m) => (
              <tr key={m.id} className="border-t border-black/10 dark:border-white/10">
                <td className="py-2">{m.thresholdCount}</td>
                <td className="py-2">
                  {m.bonusValue} ({m.bonusType === ReferralRewardType.WalletCredit ? "wallet credit" : "coupon"})
                </td>
                <td className="py-2">{m.isActive ? "Active" : "Inactive"}</td>
                {canWrite ? (
                  <td className="py-2">
                    <Button
                      type="button"
                      variant="secondary"
                      disabled={toggleMutation.isPending}
                      onClick={() => toggleMutation.mutate({ id: m.id, activate: !m.isActive })}
                    >
                      {m.isActive ? "Deactivate" : "Activate"}
                    </Button>
                  </td>
                ) : null}
              </tr>
            ))}
          </tbody>
        </table>
      )}

      {canWrite ? (
        <form
          className="flex items-end gap-3"
          onSubmit={(event) => {
            event.preventDefault();
            createMutation.mutate();
          }}
        >
          <Field
            label="Threshold"
            type="number"
            min={1}
            required
            value={threshold}
            onChange={(e) => setThreshold(e.target.value)}
          />
          <Select
            label="Bonus type"
            options={REWARD_TYPE_OPTIONS}
            value={String(bonusType)}
            onChange={(e) => setBonusType(Number(e.target.value))}
          />
          <Field
            label="Bonus value"
            type="number"
            step="0.01"
            required
            value={bonusValue}
            onChange={(e) => setBonusValue(e.target.value)}
          />
          <Button type="submit" disabled={createMutation.isPending}>
            {createMutation.isPending ? "Adding…" : "Add tier"}
          </Button>
        </form>
      ) : null}
    </div>
  );
}
