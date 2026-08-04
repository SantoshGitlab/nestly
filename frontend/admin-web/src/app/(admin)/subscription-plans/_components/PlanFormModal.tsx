"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useEffect } from "react";
import { Controller, useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, CheckboxField, Field, Modal, Select, Textarea } from "@/components/ui";
import { FormActions, FormGrid } from "@/components/data-table";
import {
  BILLING_CYCLE_OPTIONS,
  SubscriptionBillingCycle,
  type SubscriptionPlan,
  type SubscriptionPlanRequest,
} from "../_lib/plans-api";

const planSchema = z.object({
  name: z.string().min(1, "Plan name is required").max(200),
  description: z.string().max(1000),
  price: z.number({ message: "Enter a price" }).min(0, "Price cannot be negative"),
  billingCycle: z.nativeEnum(SubscriptionBillingCycle),
  freeVisitsIncluded: z
    .number({ message: "Enter a number of visits" })
    .int("Whole visits only")
    .min(0, "Cannot be negative"),
  discountPercent: z
    .number({ message: "Enter a discount" })
    .min(0, "Cannot be negative")
    .max(100, "Cannot exceed 100%"),
  prioritySlotFlag: z.boolean(),
});

type PlanFormValues = z.infer<typeof planSchema>;

const EMPTY_FORM: PlanFormValues = {
  name: "",
  description: "",
  price: 0,
  billingCycle: SubscriptionBillingCycle.Monthly,
  freeVisitsIncluded: 0,
  discountPercent: 0,
  prioritySlotFlag: false,
};

function toFormValues(plan: SubscriptionPlan | null): PlanFormValues {
  if (!plan) return EMPTY_FORM;
  return {
    name: plan.name,
    description: plan.description ?? "",
    price: plan.price,
    billingCycle: plan.billingCycle,
    freeVisitsIncluded: plan.freeVisitsIncluded,
    discountPercent: plan.discountPercent,
    prioritySlotFlag: plan.prioritySlotFlag,
  };
}

/**
 * Create/edit a subscription plan in a dialog rather than a second card below
 * the list. The previous inline form put "Edit" 900px away from the row it
 * belonged to, and reused one piece of state for creating and editing, so a
 * failed save left the create form holding the edited plan's values.
 *
 * The dialog stays open on failure with the values intact — closing it would
 * discard everything the admin typed.
 */
export function PlanFormModal({
  open,
  plan,
  isSubmitting,
  submitError,
  onSubmit,
  onClose,
}: {
  open: boolean;
  /** `null` creates a new plan. */
  plan: SubscriptionPlan | null;
  isSubmitting: boolean;
  submitError: string | null;
  onSubmit: (request: SubscriptionPlanRequest) => void;
  onClose: () => void;
}) {
  const form = useForm<PlanFormValues>({
    resolver: zodResolver(planSchema),
    defaultValues: toFormValues(plan),
  });

  // Re-seed whenever the dialog opens on a different record; the component
  // stays mounted between openings so the constructor default is not enough.
  const { reset } = form;
  useEffect(() => {
    if (open) reset(toFormValues(plan));
  }, [open, plan, reset]);

  const submit = form.handleSubmit((values) =>
    onSubmit({
      name: values.name.trim(),
      description: values.description.trim() || null,
      price: values.price,
      billingCycle: values.billingCycle,
      freeVisitsIncluded: values.freeVisitsIncluded,
      discountPercent: values.discountPercent,
      prioritySlotFlag: values.prioritySlotFlag,
    }),
  );

  return (
    <Modal
      open={open}
      // Ignore a backdrop/Escape close while the save is in flight, so the
      // admin cannot lose the form to a stray click mid-request.
      onClose={isSubmitting ? () => {} : onClose}
      title={plan ? `Edit ${plan.name}` : "New plan"}
      description={
        plan
          ? "Changes apply to new subscriptions; existing subscribers keep the terms they signed up on."
          : "A new plan is created inactive — activate it from the list when it is ready to sell."
      }
      size="lg"
    >
      <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
        {submitError ? <Alert>{submitError}</Alert> : null}

        <FormGrid columns={2}>
          <Field
            label="Name"
            required
            error={form.formState.errors.name?.message}
            {...form.register("name")}
          />
          <Controller
            control={form.control}
            name="billingCycle"
            render={({ field }) => (
              <Select
                label="Billing cycle"
                options={BILLING_CYCLE_OPTIONS}
                value={String(field.value)}
                onChange={(event) => field.onChange(Number(event.target.value))}
                error={form.formState.errors.billingCycle?.message}
              />
            )}
          />
          <Field
            label="Price"
            type="number"
            step="0.01"
            min={0}
            leading="₹"
            error={form.formState.errors.price?.message}
            {...form.register("price", { valueAsNumber: true })}
          />
          <Field
            label="Free visits included"
            type="number"
            min={0}
            error={form.formState.errors.freeVisitsIncluded?.message}
            {...form.register("freeVisitsIncluded", { valueAsNumber: true })}
          />
          <Field
            label="Discount percent"
            type="number"
            step="0.01"
            min={0}
            max={100}
            hint="Applied to every booking made on this plan."
            error={form.formState.errors.discountPercent?.message}
            {...form.register("discountPercent", { valueAsNumber: true })}
          />
        </FormGrid>

        <Textarea
          label="Description"
          rows={3}
          hint="Shown to customers on the plan comparison."
          error={form.formState.errors.description?.message}
          {...form.register("description")}
        />

        <Controller
          control={form.control}
          name="prioritySlotFlag"
          render={({ field }) => (
            <CheckboxField
              label="Priority slot access"
              description="Subscribers on this plan can book slots held back from general availability."
              checked={field.value}
              onChange={field.onChange}
            />
          )}
        />

        <FormActions>
          <Button type="button" variant="secondary" onClick={onClose} disabled={isSubmitting}>
            Cancel
          </Button>
          <Button type="submit" loading={isSubmitting}>
            {plan ? "Save changes" : "Create plan"}
          </Button>
        </FormActions>
      </form>
    </Modal>
  );
}
