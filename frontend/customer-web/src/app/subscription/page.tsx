"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import {
  BannerBreadcrumb,
  DetailList,
  DetailRow,
  ScreenSkeleton,
  formatInstantDate,
  inr,
} from "@/components/patterns";
import { Reveal, RevealItem } from "@/components/motion";
import { PageBanner } from "@/components/PageBanner";
import { RequireAuth } from "@/components/RequireAuth";
import {
  Alert,
  Badge,
  Button,
  Card,
  EmptyState,
  Modal,
  Skeleton,
  useToast,
} from "@/components/ui";
import type { BadgeTone } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import { CustomerSubscriptionStatus, SubscriptionBillingCycle } from "@/lib/types";
import type { MySubscriptionResponse, SubscriptionPlanBrowseResponse } from "@/lib/types";

const MY_SUBSCRIPTION_KEY = ["my-subscription"] as const;

/** "per month" reads better than "per Monthly" next to a price. */
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
      return "Payment failed — retrying";
    default:
      return "Unknown";
  }
}

/**
 * `PaymentFailed` is `warning`, not `danger`: the backend keeps retrying and
 * the benefits are still live, so it is a state that needs attention rather
 * than one that has already gone wrong for the customer.
 */
function statusTone(status: CustomerSubscriptionStatus): BadgeTone {
  switch (status) {
    case CustomerSubscriptionStatus.Active:
      return "success";
    case CustomerSubscriptionStatus.PaymentFailed:
      return "warning";
    case CustomerSubscriptionStatus.Cancelled:
    case CustomerSubscriptionStatus.Expired:
      return "neutral";
    default:
      return "neutral";
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
  const query = useQuery({
    queryKey: MY_SUBSCRIPTION_KEY,
    queryFn: async () => {
      const subscription = await apiFetch<MySubscriptionResponse | undefined>(
        `${API_V1}/subscription/me`,
        { authenticated: true },
      );
      /**
       * GET /subscription/me answers **204** for a customer with no live
       * subscription, and `apiFetch` maps that to `undefined`. React Query
       * treats an `undefined` resolution as a failure — it throws
       * "<key> data is undefined" — so this query used to land in the *error*
       * state for every customer who has never subscribed, showing them a
       * generic failure instead of the plan picker. That is precisely the
       * audience the screen exists for. Normalising to `null` makes "no
       * subscription" a value the UI can branch on.
       */
      return subscription ?? null;
    },
  });

  if (query.isPending) {
    return (
      <main className="flex w-full flex-col" aria-hidden>
        <div className="listing-banner h-[13.5rem] w-full sm:h-[15.5rem]" />
        <ScreenSkeleton cards={2} className="mx-auto w-full max-w-7xl px-4 py-10 sm:px-6 sm:py-14" />
      </main>
    );
  }

  if (query.isError) {
    return (
      <main className="mx-auto w-full max-w-7xl px-4 py-8 sm:px-6 sm:py-12">
        <Alert
          tone="error"
          title="Couldn't load your subscription"
          action={
            <Button size="sm" variant="secondary" onClick={() => query.refetch()}>
              Retry
            </Button>
          }
        >
          {describeError(query.error)}
        </Alert>
      </main>
    );
  }

  return query.data ? <MySubscriptionView subscription={query.data} /> : <PlanSelectionView />;
}

/* -------------------------------------------------------------------------- */
/* Managing a live subscription                                               */
/* -------------------------------------------------------------------------- */

function MySubscriptionView({ subscription }: { subscription: MySubscriptionResponse }) {
  const queryClient = useQueryClient();
  const toast = useToast();
  const [confirmOpen, setConfirmOpen] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Cancel is only rejected outright once the subscription is already
  // Cancelled or Expired (CustomerSubscription.Cancel).
  const canCancel =
    subscription.status === CustomerSubscriptionStatus.Active ||
    subscription.status === CustomerSubscriptionStatus.PaymentFailed;

  const cancelMutation = useMutation({
    mutationFn: () =>
      apiFetch<void>(`${API_V1}/subscription/${subscription.id}/cancel`, {
        method: "POST",
        authenticated: true,
      }),
    onSuccess: () => {
      setError(null);
      setConfirmOpen(false);
      // The refetch returns 204 once cancelled, which the query normalises to
      // null — so this view is replaced by the plan picker rather than sitting
      // on a subscription that no longer applies.
      queryClient.invalidateQueries({ queryKey: MY_SUBSCRIPTION_KEY });
      toast("success", "Subscription cancelled.");
    },
    onError: (err) => setError(describeError(err)),
  });

  return (
    <main className="flex w-full flex-col animate-rise">
      <PageBanner
        title="My subscription"
        description="Your Glavyx membership and remaining benefits."
        badge={<Badge tone={statusTone(subscription.status)}>{statusLabel(subscription.status)}</Badge>}
        breadcrumb={<BannerBreadcrumb items={[{ label: "Home", href: "/" }, { label: "My subscription" }]} />}
      />

      <div className="mx-auto w-full max-w-7xl px-4 py-10 sm:px-6 sm:py-14">
      <div className="flex flex-col gap-6">
        {subscription.status === CustomerSubscriptionStatus.PaymentFailed ? (
          <Alert tone="warning" title="We couldn't take your last payment">
            {subscription.lastPaymentFailureReason ??
              "We'll retry automatically. Your benefits stay active in the meantime."}
          </Alert>
        ) : null}

        <Card title={subscription.planName} description="What this plan includes.">
          <DetailList>
            <DetailRow label="Price" numeric>
              {inr(subscription.price)} / {billingCycleLabel(subscription.billingCycle)}
            </DetailRow>
            <DetailRow label="Free visits remaining" numeric>
              {subscription.freeVisitsRemaining} of {subscription.freeVisitsIncluded}
            </DetailRow>
            {subscription.discountPercent > 0 ? (
              <DetailRow label="Standing discount" numeric>
                {subscription.discountPercent}% off every booking
              </DetailRow>
            ) : null}
            {subscription.prioritySlotFlag ? (
              <DetailRow label="Slot access">Priority</DetailRow>
            ) : null}
          </DetailList>
        </Card>

        <Card title="Billing">
          <DetailList>
            <DetailRow label="Current period" numeric>
              {formatInstantDate(subscription.currentPeriodStartUtc)} –{" "}
              {formatInstantDate(subscription.currentPeriodEndUtc)}
            </DetailRow>
            <DetailRow label="Next billing date" numeric>
              {formatInstantDate(subscription.nextBillingDateUtc)}
            </DetailRow>
            <DetailRow label="Member since" numeric>
              {formatInstantDate(subscription.createdAtUtc)}
            </DetailRow>
            {subscription.cancelledAtUtc ? (
              <DetailRow label="Cancelled on" numeric>
                {formatInstantDate(subscription.cancelledAtUtc)}
              </DetailRow>
            ) : null}
          </DetailList>
        </Card>

        {canCancel ? (
          <Card
            title="Cancel subscription"
            description="You keep your benefits until the end of the current billing period."
          >
            {error ? (
              <div className="mb-4">
                <Alert tone="error" title="Couldn't cancel your subscription">
                  {error}
                </Alert>
              </div>
            ) : null}

            <Button type="button" variant="danger" onClick={() => setConfirmOpen(true)}>
              Cancel subscription
            </Button>
          </Card>
        ) : null}
      </div>

      {/* Cancelling cannot be undone — the domain refuses to reactivate a
          cancelled subscription, so resubscribing means starting a new billing
          period. That is far too much to hang off a single click. */}
      <Modal
        open={confirmOpen}
        onClose={() => {
          if (cancelMutation.isPending) return;
          setConfirmOpen(false);
        }}
        title="Cancel your subscription?"
        description={`${subscription.planName} will not renew, and you'll go back to standard pricing.`}
        size="sm"
        footer={
          <>
            <Button
              type="button"
              variant="secondary"
              disabled={cancelMutation.isPending}
              onClick={() => setConfirmOpen(false)}
            >
              Keep my subscription
            </Button>
            <Button
              type="button"
              variant="danger"
              loading={cancelMutation.isPending}
              onClick={() => {
                // `loading` already disables this; the guard drops a second
                // click landing before that disable has rendered.
                if (cancelMutation.isPending) return;
                cancelMutation.mutate();
              }}
            >
              Yes, cancel it
            </Button>
          </>
        }
      >
        <div className="flex flex-col gap-3 text-sm leading-relaxed text-fg-muted">
          <p>
            You&apos;ll lose{" "}
            <span className="nums font-medium text-fg">{subscription.freeVisitsRemaining}</span>{" "}
            remaining free visit{subscription.freeVisitsRemaining === 1 ? "" : "s"}
            {subscription.discountPercent > 0 ? (
              <>
                {" "}
                and your{" "}
                <span className="nums font-medium text-fg">{subscription.discountPercent}%</span>{" "}
                standing discount
              </>
            ) : null}
            .
          </p>
          <p>
            This can&apos;t be undone. Subscribing again later starts a fresh billing period at the
            plan&apos;s current price.
          </p>
        </div>
      </Modal>
      </div>
    </main>
  );
}

/* -------------------------------------------------------------------------- */
/* Choosing a plan                                                            */
/* -------------------------------------------------------------------------- */

function PlanSelectionView() {
  const queryClient = useQueryClient();
  const toast = useToast();
  const [error, setError] = useState<string | null>(null);

  const plansQuery = useQuery({
    queryKey: ["subscription-plans"],
    queryFn: () =>
      apiFetch<SubscriptionPlanBrowseResponse[]>(`${API_V1}/subscription/plans`, {
        authenticated: true,
      }),
  });

  const subscribeMutation = useMutation({
    mutationFn: (planId: string) =>
      apiFetch<MySubscriptionResponse>(`${API_V1}/subscription/subscribe`, {
        method: "POST",
        authenticated: true,
        body: JSON.stringify({ planId }),
      }),
    onSuccess: (created) => {
      setError(null);
      // Seeding the cache from the 201 body flips straight to the management
      // view; the invalidate is the safety net if the shapes ever diverge.
      queryClient.setQueryData(MY_SUBSCRIPTION_KEY, created);
      queryClient.invalidateQueries({ queryKey: MY_SUBSCRIPTION_KEY });
      toast("success", `You're subscribed to ${created.planName}.`);
    },
    onError: (err) => setError(describeError(err)),
  });

  /**
   * Which plan is being subscribed to, not merely whether *a* plan is. The
   * bare `isPending` flag put every card's button into "Subscribing…" at once,
   * so the one piece of information the customer needed — which of them they
   * had actually pressed — was the one thing the UI stopped showing.
   */
  const pendingPlanId = subscribeMutation.isPending ? subscribeMutation.variables : null;

  return (
    <main className="flex w-full flex-col animate-rise">
      <PageBanner
        title="Glavyx Plus"
        description="Subscribe for free visits and a standing discount on every booking."
        breadcrumb={<BannerBreadcrumb items={[{ label: "Home", href: "/" }, { label: "Glavyx Plus" }]} />}
      />

      <div className="mx-auto w-full max-w-7xl px-4 py-10 sm:px-6 sm:py-14">
      {error ? (
        <div className="mb-6">
          <Alert tone="error" title="Couldn't start your subscription">
            {error}
          </Alert>
        </div>
      ) : null}

      {plansQuery.isPending ? (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2" aria-hidden>
          {[0, 1].map((card) => (
            <Skeleton key={card} className="h-64 rounded-2xl" />
          ))}
        </div>
      ) : plansQuery.isError ? (
        <Alert
          tone="error"
          title="Couldn't load the plans"
          action={
            <Button size="sm" variant="secondary" onClick={() => plansQuery.refetch()}>
              Retry
            </Button>
          }
        >
          {describeError(plansQuery.error)}
        </Alert>
      ) : plansQuery.data.length === 0 ? (
        <EmptyState
          title="No plans available right now"
          description="Membership plans aren't open in your area yet. Everything on Glavyx is still bookable without one."
          action={
            <Button size="sm" variant="secondary" onClick={() => plansQuery.refetch()}>
              Check again
            </Button>
          }
        />
      ) : (
        <Reveal as="ul" className="grid list-none grid-cols-1 gap-4 sm:grid-cols-2">
          {plansQuery.data.map((plan) => (
            <RevealItem key={plan.id} className="flex">
              <PlanCard
                plan={plan}
                pending={pendingPlanId === plan.id}
                // Every button locks while any subscribe is in flight: the
                // server rejects a second subscription outright, so offering
                // one is offering a guaranteed 409.
                disabled={subscribeMutation.isPending}
                onSubscribe={() => {
                  if (subscribeMutation.isPending) return;
                  subscribeMutation.mutate(plan.id);
                }}
              />
            </RevealItem>
          ))}
        </Reveal>
      )}
      </div>
    </main>
  );
}

function PlanCard({
  plan,
  pending,
  disabled,
  onSubscribe,
}: {
  plan: SubscriptionPlanBrowseResponse;
  pending: boolean;
  disabled: boolean;
  onSubscribe: () => void;
}) {
  const benefits = [
    plan.freeVisitsIncluded > 0
      ? `${plan.freeVisitsIncluded} free visit${plan.freeVisitsIncluded === 1 ? "" : "s"} per ${billingCycleLabel(plan.billingCycle)}`
      : null,
    plan.discountPercent > 0 ? `${plan.discountPercent}% off every booking` : null,
    plan.prioritySlotFlag ? "Priority slot access" : null,
  ].filter((benefit): benefit is string => benefit !== null);

  return (
    <Card title={plan.name} description={plan.description ?? undefined} className="flex flex-col">
      <div className="flex flex-1 flex-col">
        <p className="nums text-display-sm font-semibold text-fg">
          {inr(plan.price)}
          <span className="text-sm font-normal text-fg-muted">
            {" "}
            / {billingCycleLabel(plan.billingCycle)}
          </span>
        </p>

        {benefits.length > 0 ? (
          <ul className="mt-4 flex flex-1 flex-col gap-2">
            {benefits.map((benefit) => (
              <li key={benefit} className="flex items-start gap-2 text-sm text-fg-muted">
                <CheckIcon />
                <span className="min-w-0">{benefit}</span>
              </li>
            ))}
          </ul>
        ) : null}

        <Button
          type="button"
          fullWidth
          className="mt-6"
          loading={pending}
          disabled={disabled && !pending}
          onClick={onSubscribe}
        >
          {/* The plan name stays in the accessible name so a screen reader
              hearing "Subscribe" three times can still tell them apart. */}
          Subscribe<span className="sr-only"> to {plan.name}</span>
        </Button>
      </div>
    </Card>
  );
}

function CheckIcon() {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="2.5"
      strokeLinecap="round"
      strokeLinejoin="round"
      className="mt-0.5 h-4 w-4 shrink-0 text-success"
      aria-hidden
    >
      <path d="m5 13 4 4L19 7" />
    </svg>
  );
}
