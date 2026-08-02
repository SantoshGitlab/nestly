"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { Alert, Button, Card, Field, PageHeading, Select } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import { getSessionClaims, subscribeToAuthChanges } from "@/lib/auth";
import { canWriteModule } from "@/lib/permissions";
import type { AdminSessionClaims } from "@/lib/types";

enum SubscriptionBillingCycle {
  Monthly = 0,
  Quarterly = 1,
  Yearly = 2,
}

const BILLING_CYCLE_OPTIONS = [
  { value: String(SubscriptionBillingCycle.Monthly), label: "Monthly" },
  { value: String(SubscriptionBillingCycle.Quarterly), label: "Quarterly" },
  { value: String(SubscriptionBillingCycle.Yearly), label: "Yearly" },
] as const;

interface SubscriptionPlanAdminResponse {
  id: string;
  name: string;
  description: string | null;
  price: number;
  billingCycle: SubscriptionBillingCycle;
  freeVisitsIncluded: number;
  discountPercent: number;
  prioritySlotFlag: boolean;
  isActive: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

interface PlanFormValues {
  name: string;
  description: string;
  price: number;
  billingCycle: SubscriptionBillingCycle;
  freeVisitsIncluded: number;
  discountPercent: number;
  prioritySlotFlag: boolean;
}

const EMPTY_FORM: PlanFormValues = {
  name: "",
  description: "",
  price: 0,
  billingCycle: SubscriptionBillingCycle.Monthly,
  freeVisitsIncluded: 0,
  discountPercent: 0,
  prioritySlotFlag: false,
};

const PLANS_BASE = `${API_V1}/subscription-plans`;

/**
 * Admin CRUD for subscription plans (PRODUCT-ENHANCEMENTS.md #1, task 180):
 * price, billing cycle, benefits, active window.
 */
export default function SubscriptionPlansPage() {
  const [claims, setClaims] = useState<AdminSessionClaims | null>(null);
  useEffect(() => {
    const sync = () => setClaims(getSessionClaims());
    sync();
    return subscribeToAuthChanges(sync);
  }, []);
  const canWrite = canWriteModule(claims, "subscription");

  const queryClient = useQueryClient();
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form, setForm] = useState<PlanFormValues>(EMPTY_FORM);

  const plansQuery = useQuery({
    queryKey: ["admin-subscription-plans"],
    queryFn: () => apiFetch<SubscriptionPlanAdminResponse[]>(PLANS_BASE, { authenticated: true }),
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["admin-subscription-plans"] });

  const saveMutation = useMutation({
    mutationFn: () => {
      const body = JSON.stringify({
        name: form.name,
        description: form.description || null,
        price: form.price,
        billingCycle: form.billingCycle,
        freeVisitsIncluded: form.freeVisitsIncluded,
        discountPercent: form.discountPercent,
        prioritySlotFlag: form.prioritySlotFlag,
      });
      return editingId
        ? apiFetch<SubscriptionPlanAdminResponse>(`${PLANS_BASE}/${editingId}`, { method: "PUT", authenticated: true, body })
        : apiFetch<SubscriptionPlanAdminResponse>(PLANS_BASE, { method: "POST", authenticated: true, body });
    },
    onSuccess: () => {
      setForm(EMPTY_FORM);
      setEditingId(null);
      invalidate();
    },
  });

  const toggleActiveMutation = useMutation({
    mutationFn: ({ id, activate }: { id: string; activate: boolean }) =>
      apiFetch(`${PLANS_BASE}/${id}/${activate ? "activate" : "deactivate"}`, { method: "POST", authenticated: true }),
    onSuccess: invalidate,
  });

  function startEdit(plan: SubscriptionPlanAdminResponse) {
    setEditingId(plan.id);
    setForm({
      name: plan.name,
      description: plan.description ?? "",
      price: plan.price,
      billingCycle: plan.billingCycle,
      freeVisitsIncluded: plan.freeVisitsIncluded,
      discountPercent: plan.discountPercent,
      prioritySlotFlag: plan.prioritySlotFlag,
    });
  }

  return (
    <main className="flex flex-col gap-6">
      <PageHeading title="Subscription Plans" subtitle="Price, billing cycle, and benefits for Nestly Plus tiers." />

      <Card title="Plans">
        {plansQuery.isPending ? (
          <p className="text-sm text-neutral-500">Loading…</p>
        ) : plansQuery.isError ? (
          <Alert>{describeError(plansQuery.error)}</Alert>
        ) : plansQuery.data.length === 0 ? (
          <p className="text-sm text-neutral-600 dark:text-neutral-400">No plans yet.</p>
        ) : (
          <table className="w-full text-left text-sm">
            <thead className="text-neutral-600 dark:text-neutral-400">
              <tr>
                <th className="pb-2">Name</th>
                <th className="pb-2">Price</th>
                <th className="pb-2">Cycle</th>
                <th className="pb-2">Free visits</th>
                <th className="pb-2">Discount</th>
                <th className="pb-2">Status</th>
                {canWrite ? <th className="pb-2">Actions</th> : null}
              </tr>
            </thead>
            <tbody>
              {plansQuery.data.map((plan) => (
                <tr key={plan.id} className="border-t border-black/10 dark:border-white/10">
                  <td className="py-2">{plan.name}</td>
                  <td className="py-2">₹{plan.price.toFixed(2)}</td>
                  <td className="py-2">{BILLING_CYCLE_OPTIONS.find((o) => Number(o.value) === plan.billingCycle)?.label}</td>
                  <td className="py-2">{plan.freeVisitsIncluded}</td>
                  <td className="py-2">{plan.discountPercent}%</td>
                  <td className="py-2">{plan.isActive ? "Active" : "Inactive"}</td>
                  {canWrite ? (
                    <td className="py-2">
                      <div className="flex gap-2">
                        <Button type="button" variant="secondary" onClick={() => startEdit(plan)}>
                          Edit
                        </Button>
                        <Button
                          type="button"
                          variant="secondary"
                          disabled={toggleActiveMutation.isPending}
                          onClick={() => toggleActiveMutation.mutate({ id: plan.id, activate: !plan.isActive })}
                        >
                          {plan.isActive ? "Deactivate" : "Activate"}
                        </Button>
                      </div>
                    </td>
                  ) : null}
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </Card>

      {canWrite ? (
        <Card title={editingId ? "Edit plan" : "New plan"}>
          {saveMutation.isError ? (
            <div className="mb-3">
              <Alert>{describeError(saveMutation.error)}</Alert>
            </div>
          ) : null}
          <form
            className="flex flex-col gap-4"
            onSubmit={(event) => {
              event.preventDefault();
              saveMutation.mutate();
            }}
          >
            <div className="grid grid-cols-2 gap-4">
              <Field label="Name" required value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
              <Select
                label="Billing cycle"
                options={BILLING_CYCLE_OPTIONS}
                value={String(form.billingCycle)}
                onChange={(e) => setForm({ ...form, billingCycle: Number(e.target.value) })}
              />
              <Field
                label="Price"
                type="number"
                step="0.01"
                value={form.price}
                onChange={(e) => setForm({ ...form, price: Number(e.target.value) })}
              />
              <Field
                label="Free visits included"
                type="number"
                value={form.freeVisitsIncluded}
                onChange={(e) => setForm({ ...form, freeVisitsIncluded: Number(e.target.value) })}
              />
              <Field
                label="Discount percent"
                type="number"
                step="0.01"
                value={form.discountPercent}
                onChange={(e) => setForm({ ...form, discountPercent: Number(e.target.value) })}
              />
            </div>
            <div>
              <label htmlFor="plan-description" className="text-sm font-medium">
                Description
              </label>
              <textarea
                id="plan-description"
                rows={2}
                value={form.description}
                onChange={(e) => setForm({ ...form, description: e.target.value })}
                className="mt-1.5 w-full rounded-lg border border-black/15 bg-transparent px-3 py-2 text-sm outline-none focus:border-black focus:ring-1 focus:ring-black dark:border-white/20 dark:focus:border-white dark:focus:ring-white"
              />
            </div>
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={form.prioritySlotFlag}
                onChange={(e) => setForm({ ...form, prioritySlotFlag: e.target.checked })}
              />
              Priority slot access
            </label>
            <div className="flex gap-2">
              <Button type="submit" disabled={saveMutation.isPending || form.name.trim() === ""} className="w-fit">
                {saveMutation.isPending ? "Saving…" : editingId ? "Save changes" : "Create plan"}
              </Button>
              {editingId ? (
                <Button
                  type="button"
                  variant="secondary"
                  className="w-fit"
                  onClick={() => {
                    setEditingId(null);
                    setForm(EMPTY_FORM);
                  }}
                >
                  Cancel edit
                </Button>
              ) : null}
            </div>
          </form>
        </Card>
      ) : null}
    </main>
  );
}
