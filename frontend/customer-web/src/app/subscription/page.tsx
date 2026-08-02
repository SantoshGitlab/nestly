"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { RequireAuth } from "@/components/RequireAuth";
import { Alert, Button, Card, PageHeading } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import { CustomerSubscriptionStatus, SubscriptionBillingCycle } from "@/lib/types";
import type { MySubscriptionResponse, SubscriptionPlanBrowseResponse } from "@/lib/types";

function billingCycleLabel(cycle: SubscriptionBillingCycle): string {
  switch (cycle) {
    case SubscriptionBillingCycle.Monthly:
      return "month";
    case SubscriptionBillingCycle.Quarterly:
      return "quarter";
    case SubscriptionBillingCycle.Yearly:
      return "year";
    default:
      return "cycle";
  }
}

function statusLabel(status: CustomerSubscriptionStatus): string {
  switch (status) {
    case CustomerSubscriptionStatus.Active:
      return "Active";
    case CustomerSubscriptionStatus.Cancelled:
      return "Cancelled";
    case CustomerSubscriptionStatus.Expired:
      return "Expired";
    case CustomerSubscriptionStatus.PaymentFailed:
      return "Payment failed - retrying";
    default:
      return "Unknown";
  }
}

/**
 * Subscription / membership plans (PRODUCT-ENHANCEMENTS.md #1, task 182):
 * plan selection for a customer with no live subscription, my-subscription
 * management once they have one.
 */
export default function SubscriptionPage() {
  return (
    <RequireAuth>
      <SubscriptionScreen />
    </RequireAuth>
  );
}

function SubscriptionScreen() {
  const mySubscriptionQuery = useQuery({
    queryKey: ["my-subscription"],
    queryFn: () => apiFetch<MySubscriptionResponse | undefined>(`${API_V1}/subscription/me`, { authenticated: true }),
  });

  if (mySubscriptionQuery.isPending) {
    return <main className="mx-auto w-full max-w-2xl px-6 py-12 text-sm text-neutral-500">Loading…</main>;
  }

  if (mySubscriptionQuery.isError) {
    return (
      <main className="mx-auto w-full max-w-2xl px-6 py-12">
        <Alert>{describeError(mySubscriptionQuery.error)}</Alert>
      </main>
    );
  }

  return mySubscriptionQuery.data ? (
    <MySubscriptionView subscription={mySubscriptionQuery.data} />
  ) : (
    <PlanSelectionView />
  );
}

function MySubscriptionView({ subscription }: { subscription: MySubscriptionResponse }) {
  const queryClient = useQueryClient();
  const canCancel = subscription.status === CustomerSubscriptionStatus.Active || subscription.status === CustomerSubscriptionStatus.PaymentFailed;

  const cancelMutation = useMutation({
    mutationFn: () => apiFetch(`${API_V1}/subscription/${subscription.id}/cancel`, { method: "POST", authenticated: true }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["my-subscription"] }),
  });

  return (
    <main className="mx-auto w-full max-w-2xl px-6 py-12">
      <PageHeading title="My subscription" subtitle="Your Nestly membership and remaining benefits." />

      {cancelMutation.isError ? (
        <div className="mb-4">
          <Alert>{describeError(cancelMutation.error)}</Alert>
        </div>
      ) : null}

      <Card title={subscription.planName}>
        <dl className="flex flex-col gap-3 text-sm">
          <div className="flex items-center justify-between">
            <dt className="text-neutral-600 dark:text-neutral-400">Status</dt>
            <dd className="font-medium">{statusLabel(subscription.status)}</dd>
          </div>
          <div className="flex items-center justify-between">
            <dt className="text-neutral-600 dark:text-neutral-400">Price</dt>
            <dd className="font-medium">
              ₹{subscription.price.toFixed(2)} / {billingCycleLabel(subscription.billingCycle)}
            </dd>
          </div>
          <div className="flex items-center justify-between">
            <dt className="text-neutral-600 dark:text-neutral-400">Free visits remaining</dt>
            <dd className="font-medium">{subscription.freeVisitsRemaining}</dd>
          </div>
          {subscription.discountPercent > 0 ? (
            <div className="flex items-center justify-between">
              <dt className="text-neutral-600 dark:text-neutral-400">Standing discount</dt>
              <dd className="font-medium">{subscription.discountPercent}%</dd>
            </div>
          ) : null}
          <div className="flex items-center justify-between">
            <dt className="text-neutral-600 dark:text-neutral-400">Current period</dt>
            <dd className="font-medium">
              {new Date(subscription.currentPeriodStartUtc).toLocaleDateString()} – {new Date(subscription.currentPeriodEndUtc).toLocaleDateString()}
            </dd>
          </div>
          <div className="flex items-center justify-between">
            <dt className="text-neutral-600 dark:text-neutral-400">Next billing date</dt>
            <dd className="font-medium">{new Date(subscription.nextBillingDateUtc).toLocaleDateString()}</dd>
          </div>
          {subscription.lastPaymentFailureReason ? (
            <div>
              <dt className="text-neutral-600 dark:text-neutral-400">Last payment issue</dt>
              <dd className="mt-1">{subscription.lastPaymentFailureReason}</dd>
            </div>
          ) : null}
        </dl>

        {canCancel ? (
          <div className="mt-5">
            <Button type="button" variant="danger" disabled={cancelMutation.isPending} onClick={() => cancelMutation.mutate()}>
              {cancelMutation.isPending ? "Cancelling…" : "Cancel subscription"}
            </Button>
          </div>
        ) : null}
      </Card>
    </main>
  );
}

function PlanSelectionView() {
  const queryClient = useQueryClient();

  const plansQuery = useQuery({
    queryKey: ["subscription-plans"],
    queryFn: () => apiFetch<SubscriptionPlanBrowseResponse[]>(`${API_V1}/subscription/plans`, { authenticated: true }),
  });

  const subscribeMutation = useMutation({
    mutationFn: (planId: string) =>
      apiFetch(`${API_V1}/subscription/subscribe`, {
        method: "POST",
        authenticated: true,
        body: JSON.stringify({ planId }),
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["my-subscription"] }),
  });

  return (
    <main className="mx-auto w-full max-w-3xl px-6 py-12">
      <PageHeading title="Nestly Plus" subtitle="Subscribe for free visits and a standing discount on every booking." />

      {subscribeMutation.isError ? (
        <div className="mb-4">
          <Alert>{describeError(subscribeMutation.error)}</Alert>
        </div>
      ) : null}

      {plansQuery.isPending ? (
        <p className="text-sm text-neutral-500">Loading plans…</p>
      ) : plansQuery.isError ? (
        <Alert>{describeError(plansQuery.error)}</Alert>
      ) : plansQuery.data.length === 0 ? (
        <Card title="No plans available">
          <p className="text-sm text-neutral-600 dark:text-neutral-400">Check back soon for membership plans.</p>
        </Card>
      ) : (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
          {plansQuery.data.map((plan) => (
            <Card key={plan.id} title={plan.name} description={plan.description ?? undefined}>
              <p className="text-2xl font-semibold">
                ₹{plan.price.toFixed(2)}
                <span className="text-sm font-normal text-neutral-500"> / {billingCycleLabel(plan.billingCycle)}</span>
              </p>
              <ul className="mt-3 flex flex-col gap-1 text-sm text-neutral-600 dark:text-neutral-400">
                {plan.freeVisitsIncluded > 0 ? <li>{plan.freeVisitsIncluded} free visits per cycle</li> : null}
                {plan.discountPercent > 0 ? <li>{plan.discountPercent}% off every booking</li> : null}
                {plan.prioritySlotFlag ? <li>Priority slot access</li> : null}
              </ul>
              <div className="mt-4">
                <Button
                  type="button"
                  disabled={subscribeMutation.isPending}
                  onClick={() => subscribeMutation.mutate(plan.id)}
                  className="w-full"
                >
                  {subscribeMutation.isPending ? "Subscribing…" : "Subscribe"}
                </Button>
              </div>
            </Card>
          ))}
        </div>
      )}
    </main>
  );
}
