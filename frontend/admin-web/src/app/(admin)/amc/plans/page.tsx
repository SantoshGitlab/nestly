"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { Button, PageHeading, useToast } from "@/components/ui";
import { SubscriptionTabs } from "@/components/SubscriptionTabs";
import { describeError } from "@/lib/api";
import { listCategories } from "@/lib/catalog-api";
import { canWriteModule } from "@/lib/permissions";
import { useAdminClaims } from "@/lib/use-admin-claims";
import { AmcPlanFormModal } from "../_components/AmcPlanFormModal";
import { AmcPlansTable } from "../_components/AmcPlansTable";
import {
  createAmcPlan,
  listAmcPlans,
  setAmcPlanActive,
  updateAmcPlan,
  type AmcPlanAdminResponse,
  type AmcPlanRequest,
} from "../_lib/amc-api";

const PLANS_QUERY_KEY = ["admin-amc-plans"] as const;

/**
 * Admin CRUD for AMC plans (docs/AMC.md, Phase 20): category, price, term,
 * visits included. Structured identically to `SubscriptionPlansPage` -
 * mutating controls gated on "subscription.write" (AmcPlansController mirrors
 * SubscriptionPlansController's own RBAC exactly, per docs/AMC.md's RBAC
 * ADDITIONS), the route reachable once `AdminSidebar` has filtered the
 * "Subscription Plans" entry in by "subscription.read", and the API
 * authorises every call again server-side.
 */
export default function AmcPlansPage() {
  const claims = useAdminClaims();
  const canWrite = canWriteModule(claims, "subscription");
  const queryClient = useQueryClient();
  const toast = useToast();

  const [formOpen, setFormOpen] = useState(false);
  const [editingPlan, setEditingPlan] = useState<AmcPlanAdminResponse | null>(null);
  const [formError, setFormError] = useState<string | null>(null);

  const plansQuery = useQuery({ queryKey: PLANS_QUERY_KEY, queryFn: listAmcPlans });
  const categoriesQuery = useQuery({ queryKey: ["categories"], queryFn: listCategories });

  const categoryOptions = (categoriesQuery.data ?? [])
    .filter((category) => category.isActive)
    .map((category) => ({ value: category.id, label: category.name }));

  const invalidatePlans = () => queryClient.invalidateQueries({ queryKey: PLANS_QUERY_KEY });

  const closeForm = () => {
    setFormOpen(false);
    setEditingPlan(null);
    setFormError(null);
  };

  const saveMutation = useMutation({
    mutationFn: (request: AmcPlanRequest) =>
      editingPlan ? updateAmcPlan(editingPlan.id, request) : createAmcPlan(request),
    onSuccess: (_plan, request) => {
      void invalidatePlans();
      toast("success", editingPlan ? `${request.name} updated.` : `${request.name} created.`);
      closeForm();
    },
    onError: (error) => setFormError(describeError(error)),
  });

  const toggleMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setAmcPlanActive(id, isActive),
    onSuccess: () => void invalidatePlans(),
  });

  const openCreate = () => {
    setEditingPlan(null);
    setFormError(null);
    setFormOpen(true);
  };

  const openEdit = (plan: AmcPlanAdminResponse) => {
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
    <div className="flex w-full max-w-6xl flex-col gap-6">
      <PageHeading
        title="Subscription Plans"
        subtitle="AMC plans: prepaid entitlement to a fixed number of service visits for one appliance, over a fixed term (docs/AMC.md)."
        actions={newPlanButton}
      />

      <SubscriptionTabs />

      <AmcPlansTable
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

      <AmcPlanFormModal
        open={formOpen}
        plan={editingPlan}
        categoryOptions={categoryOptions}
        isSubmitting={saveMutation.isPending}
        submitError={formError}
        onSubmit={(request) => saveMutation.mutate(request)}
        onClose={closeForm}
      />
    </div>
  );
}
