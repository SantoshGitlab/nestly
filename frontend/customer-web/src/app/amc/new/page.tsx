"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { BannerBreadcrumb, inr } from "@/components/patterns";
import { Reveal, RevealItem } from "@/components/motion";
import { PageBanner } from "@/components/PageBanner";
import { RequireAuth } from "@/components/RequireAuth";
import {
  Alert,
  Button,
  Card,
  EmptyState,
  Field,
  Modal,
  Skeleton,
  useToast,
} from "@/components/ui";
import { describeError } from "@/lib/api";
import { browseAmcPlans, purchaseAmcContract } from "@/lib/amc-api";
import type { AmcPlanBrowseResponse } from "@/lib/types";

/**
 * Browse the AMC plan catalog and purchase a contract for a named asset
 * (docs/AMC.md "HOW IT WORKS"). There is deliberately no payment step here -
 * per docs/AMC.md OPEN DECISIONS #4, purchase does not charge a real payment
 * gateway order yet (`CustomerAmcContract.PaymentTransactionId` is left
 * null); the confirm dialog says so plainly rather than implying a charge
 * that doesn't happen.
 */
export default function NewAmcContractPage() {
  return (
    <RequireAuth>
      <PlanBrowseScreen />
    </RequireAuth>
  );
}

function PlanBrowseScreen() {
  const router = useRouter();
  const toast = useToast();
  const [purchasingPlan, setPurchasingPlan] = useState<AmcPlanBrowseResponse | null>(null);
  const [assetLabel, setAssetLabel] = useState("");
  const [assetLabelError, setAssetLabelError] = useState<string | null>(null);

  const plansQuery = useQuery({
    queryKey: ["amc-plans"],
    queryFn: () => browseAmcPlans(),
  });

  const purchaseMutation = useMutation({
    mutationFn: () => {
      if (!purchasingPlan) throw new Error("No plan selected.");
      return purchaseAmcContract({ planId: purchasingPlan.id, assetLabel: assetLabel.trim() });
    },
    onSuccess: (created) => {
      toast("success", `${created.assetLabel} is now covered by ${created.planName}.`);
      router.push(`/amc/${created.id}`);
    },
  });

  const closeDialog = () => {
    if (purchaseMutation.isPending) return;
    setPurchasingPlan(null);
    setAssetLabel("");
    setAssetLabelError(null);
    purchaseMutation.reset();
  };

  const confirmPurchase = () => {
    const trimmed = assetLabel.trim();
    if (!trimmed) {
      setAssetLabelError("Tell us which appliance this covers, e.g. \"Living room split AC\".");
      return;
    }
    setAssetLabelError(null);
    purchaseMutation.mutate();
  };

  return (
    <main className="flex w-full flex-col">
      <PageBanner
        title="AMC plans"
        description="Pay once for a fixed number of service visits against one appliance, over a fixed term."
        breadcrumb={
          <BannerBreadcrumb
            items={[{ label: "Home", href: "/" }, { label: "My AMC contracts", href: "/amc" }, { label: "AMC plans" }]}
          />
        }
      />

      <div className="mx-auto w-full max-w-7xl px-4 py-10 sm:px-6 sm:py-14">
      {plansQuery.isPending ? (
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2" aria-hidden>
          {[0, 1].map((card) => (
            <Skeleton key={card} className="h-56 rounded-2xl" />
          ))}
        </div>
      ) : plansQuery.isError ? (
        <Alert
          tone="error"
          title="Couldn't load AMC plans"
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
          title="No AMC plans available right now"
          description="Maintenance contracts aren't open for your appliances yet — check back soon."
        />
      ) : (
        <Reveal as="ul" className="grid list-none grid-cols-1 gap-4 sm:grid-cols-2">
          {plansQuery.data.map((plan) => (
            <RevealItem key={plan.id} className="flex">
              <PlanCard plan={plan} onBuy={() => setPurchasingPlan(plan)} />
            </RevealItem>
          ))}
        </Reveal>
      )}

      <Modal
        open={purchasingPlan !== null}
        onClose={closeDialog}
        title={purchasingPlan ? `Activate ${purchasingPlan.name}` : ""}
        description="No payment gateway is wired up yet — activating records your contract at no charge."
        size="sm"
        footer={
          <>
            <Button type="button" variant="secondary" disabled={purchaseMutation.isPending} onClick={closeDialog}>
              Cancel
            </Button>
            <Button type="button" loading={purchaseMutation.isPending} onClick={confirmPurchase}>
              Activate
            </Button>
          </>
        }
      >
        {purchasingPlan ? (
          <div className="flex flex-col gap-4">
            <p className="text-sm leading-relaxed text-fg-muted">
              {purchasingPlan.visitsIncluded} visit{purchasingPlan.visitsIncluded === 1 ? "" : "s"} over{" "}
              {purchasingPlan.termMonths} month{purchasingPlan.termMonths === 1 ? "" : "s"}, for{" "}
              <span className="nums font-medium text-fg">{inr(purchasingPlan.price)}</span>.
            </p>

            <Field
              label="Which appliance does this cover?"
              placeholder="e.g. Living room split AC"
              hint="Lets you tell contracts apart if you cover more than one appliance."
              value={assetLabel}
              maxLength={150}
              error={assetLabelError ?? undefined}
              onChange={(event) => setAssetLabel(event.target.value)}
            />

            {purchaseMutation.isError ? (
              <Alert tone="error">{describeError(purchaseMutation.error)}</Alert>
            ) : null}
          </div>
        ) : null}
      </Modal>
      </div>
    </main>
  );
}

function PlanCard({ plan, onBuy }: { plan: AmcPlanBrowseResponse; onBuy: () => void }) {
  return (
    <Card title={plan.name} description={plan.description ?? undefined} className="flex flex-col">
      <div className="flex flex-1 flex-col">
        <p className="text-xs font-medium uppercase tracking-wide text-fg-subtle">{plan.categoryName}</p>
        <p className="nums mt-2 text-display-sm font-semibold text-fg">
          {inr(plan.price)}
          <span className="text-sm font-normal text-fg-muted"> / {plan.termMonths}-month term</span>
        </p>

        <ul className="mt-4 flex flex-1 flex-col gap-2">
          <li className="flex items-start gap-2 text-sm text-fg-muted">
            <CheckIcon />
            <span>
              {plan.visitsIncluded} service visit{plan.visitsIncluded === 1 ? "" : "s"} included
            </span>
          </li>
          <li className="flex items-start gap-2 text-sm text-fg-muted">
            <CheckIcon />
            <span>Redeem any time within the term — no per-visit charge</span>
          </li>
        </ul>

        <Button type="button" fullWidth className="mt-6" onClick={onBuy}>
          Activate<span className="sr-only"> {plan.name}</span>
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
