import { API_V1, apiFetch } from "@/lib/api";

/**
 * Typed client for the admin subscription-plan surface
 * (PRODUCT-ENHANCEMENTS.md #1, task 180): list, create, update, and the
 * activate/deactivate pair.
 *
 * Lives in the module rather than `src/lib` because nothing else consumes it.
 */

/** Mirrors the backend enum's declaration order exactly. */
export enum SubscriptionBillingCycle {
  Monthly = 0,
  Quarterly = 1,
  Yearly = 2,
}

const BILLING_CYCLE_LABELS: Record<SubscriptionBillingCycle, string> = {
  [SubscriptionBillingCycle.Monthly]: "Monthly",
  [SubscriptionBillingCycle.Quarterly]: "Quarterly",
  [SubscriptionBillingCycle.Yearly]: "Yearly",
};

export const BILLING_CYCLE_OPTIONS = [
  SubscriptionBillingCycle.Monthly,
  SubscriptionBillingCycle.Quarterly,
  SubscriptionBillingCycle.Yearly,
].map((cycle) => ({ value: String(cycle), label: BILLING_CYCLE_LABELS[cycle] }));

export function billingCycleLabel(cycle: SubscriptionBillingCycle): string {
  return BILLING_CYCLE_LABELS[cycle] ?? "Unknown";
}

export interface SubscriptionPlan {
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

export interface SubscriptionPlanRequest {
  name: string;
  description: string | null;
  price: number;
  billingCycle: SubscriptionBillingCycle;
  freeVisitsIncluded: number;
  discountPercent: number;
  prioritySlotFlag: boolean;
}

const PLANS_BASE = `${API_V1}/subscription-plans`;

export const listSubscriptionPlans = () =>
  apiFetch<SubscriptionPlan[]>(PLANS_BASE, { authenticated: true });

export const createSubscriptionPlan = (request: SubscriptionPlanRequest) =>
  apiFetch<SubscriptionPlan>(PLANS_BASE, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const updateSubscriptionPlan = (id: string, request: SubscriptionPlanRequest) =>
  apiFetch<SubscriptionPlan>(`${PLANS_BASE}/${id}`, {
    method: "PUT",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const setSubscriptionPlanActive = (id: string, isActive: boolean) =>
  apiFetch<void>(`${PLANS_BASE}/${id}/${isActive ? "activate" : "deactivate"}`, {
    method: "POST",
    authenticated: true,
  });
