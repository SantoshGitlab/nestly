"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { Button, PageHeading, useToast } from "@/components/ui";
import { SubscriptionTabs } from "@/components/SubscriptionTabs";
import { describeError } from "@/lib/api";
import { canWriteModule } from "@/lib/permissions";
import { useAdminClaims } from "@/lib/use-admin-claims";
import { PlanFormModal } from "./_components/PlanFormModal";
import { PlansTable } from "./_components/PlansTable";
import {
  createSubscriptionPlan,
  listSubscriptionPlans,
  setSubscriptionPlanActive,
  updateSubscriptionPlan,
  type SubscriptionPlan,
  type SubscriptionPlanRequest,
} from "./_lib/plans-api";

const PLANS_QUERY_KEY = ["admin-subscription-plans"] as const;

/**
 * Admin CRUD for subscription plans (PRODUCT-ENHANCEMENTS.md #1, task 180):
 * price, billing cycle, benefits, and the active window.
 *
 * Mutating controls are gated on "subscription.write" exactly the way every
 * other admin module gates its own; the route itself is only reachable once
 * `AdminSidebar` has already filtered it in by "subscription.read", and the
 * API authorises every call again server-side.
 */
export default function SubscriptionPlansPage() {
  const claims = useAdminClaims();
  const canWrite = canWriteModule(claims, "subscription");
  const queryClient = useQueryClient();
  const toast = useToast();

  const [formOpen, setFormOpen] = useState(false);
  const [editingPlan, setEditingPlan] = useState<SubscriptionPlan | null>(null);
  const [formError, setFormError] = useState<string | null>(null);

  const plansQuery = useQuery({ queryKey: PLANS_QUERY_KEY, queryFn: listSubscriptionPlans });

  const invalidatePlans = () => queryClient.invalidateQueries({ queryKey: PLANS_QUERY_KEY });

  const closeForm = () => {
    setFormOpen(false);
    setEditingPlan(null);
    setFormError(null);
  };

  const saveMutation = useMutation({
    mutationFn: (request: SubscriptionPlanRequest) =>
      editingPlan
        ? updateSubscriptionPlan(editingPlan.id, request)
        : createSubscriptionPlan(request),
    onSuccess: (_plan, request) => {
      void invalidatePlans();
      toast("success", editingPlan ? `${request.name} updated.` : `${request.name} created.`);
      closeForm();
    },
    // Keeps the dialog open with everything the admin typed still in it.
    onError: (error) => setFormError(describeError(error)),
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) =>
      setSubscriptionPlanActive(id, isActive),
    // The list is the only source of truth for `isActive`, so it must be
    // refetched — without this the row kept showing its previous state until
    // the next full page load.
    onSuccess: () => void invalidatePlans(),
  });

  const openCreate = () => {
    setEditingPlan(null);
    setFormError(null);
    setFormOpen(true);
  };

  const openEdit = (plan: SubscriptionPlan) => {
    setEditingPlan(plan);
    setFormError(null);
    setFormOpen(true);
  };

  const newPlanButton = canWrite ? (
    <Button type="button" size="sm" onClick={openCreate}>
      New plan
    </Button>
  ) : undefined;

  return (
    <div className="mx-auto flex w-full max-w-6xl flex-col gap-6">
      <PageHeading
        title="Subscription Plans"
        subtitle="Price, billing cycle and benefits for the Nestly Plus tiers (PRODUCT-ENHANCEMENTS.md #1)."
        actions={newPlanButton}
      />

      <SubscriptionTabs />

      <PlansTable
        plans={plansQuery.data}
        isLoading={plansQuery.isPending}
        isFetching={plansQuery.isFetching}
        error={plansQuery.error}
        onRetry={() => void plansQuery.refetch()}
        canWrite={canWrite}
        onEdit={openEdit}
        onToggleActive={(plan) => toggleMutation.mutate({ id: plan.id, isActive: !plan.isActive })}
        togglingId={toggleMutation.isPending ? toggleMutation.variables?.id : undefined}
        toggleError={toggleMutation.error}
        emptyAction={newPlanButton}
      />

      <PlanFormModal
        open={formOpen}
        plan={editingPlan}
        isSubmitting={saveMutation.isPending}
        submitError={formError}
        onSubmit={(request) => saveMutation.mutate(request)}
        onClose={closeForm}
      />
    </div>
  );
}
