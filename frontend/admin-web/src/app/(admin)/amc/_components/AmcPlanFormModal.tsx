"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Field, Modal, Textarea } from "@/components/ui";
import { FormActions, FormGrid, SearchableSelect } from "@/components/data-table";
import type { AmcPlanAdminResponse, AmcPlanRequest } from "../_lib/amc-api";

/** Mirrors AmcPlanCreateRequestValidator/AmcPlanUpdateRequestValidator (AmcValidators.cs) exactly. */
const planSchema = z.object({
  categoryId: z.string().min(1, "Select a category"),
  name: z.string().min(1, "Plan name is required").max(150),
  description: z.string().max(500),
  price: z.number({ message: "Enter a price" }).positive("Price must be greater than zero"),
  termMonths: z
    .number({ message: "Enter a term" })
    .int("Whole months only")
    .min(1, "Must be at least 1 month")
    .max(60, "Cannot exceed 60 months"),
  visitsIncluded: z
    .number({ message: "Enter a number of visits" })
    .int("Whole visits only")
    .min(1, "Must include at least 1 visit")
    .max(52, "Cannot exceed 52 visits"),
});

type PlanFormValues = z.infer<typeof planSchema>;

const EMPTY_FORM: PlanFormValues = {
  categoryId: "",
  name: "",
  description: "",
  price: 0,
  termMonths: 12,
  visitsIncluded: 1,
};

function toFormValues(plan: AmcPlanAdminResponse | null): PlanFormValues {
  if (!plan) return EMPTY_FORM;
  return {
    categoryId: plan.categoryId,
    name: plan.name,
    description: plan.description ?? "",
    price: plan.price,
    termMonths: plan.termMonths,
    visitsIncluded: plan.visitsIncluded,
  };
}

/**
 * Create/edit an AMC plan in a dialog, mirroring
 * `subscription-plans/_components/PlanFormModal.tsx` field-for-field for the
 * shared UX (dialog stays open with values intact on a failed save) - the
 * field *set* differs because AmcPlan is scoped to one service category and
 * has a fixed term/visit count rather than a billing cycle.
 */
export function AmcPlanFormModal({
  open,
  plan,
  categoryOptions,
  isSubmitting,
  submitError,
  onSubmit,
  onClose,
}: {
  open: boolean;
  /** `null` creates a new plan. */
  plan: AmcPlanAdminResponse | null;
  categoryOptions: readonly { value: string; label: string }[];
  isSubmitting: boolean;
  submitError: string | null;
  onSubmit: (request: AmcPlanRequest) => void;
  onClose: () => void;
}) {
  const form = useForm<PlanFormValues>({
    resolver: zodResolver(planSchema),
    defaultValues: toFormValues(plan),
  });

  const { reset } = form;
  useEffect(() => {
    if (open) reset(toFormValues(plan));
  }, [open, plan, reset]);

  const submit = form.handleSubmit((values) =>
    onSubmit({
      categoryId: values.categoryId,
      name: values.name.trim(),
      description: values.description.trim() || null,
      price: values.price,
      termMonths: values.termMonths,
      visitsIncluded: values.visitsIncluded,
    }),
  );

  return (
    <Modal
      open={open}
      onClose={isSubmitting ? () => {} : onClose}
      title={plan ? `Edit ${plan.name}` : "New AMC plan"}
      description={
        plan
          ? "Changes apply to new purchases; existing contracts keep the terms they were sold on (docs/AMC.md's snapshot rule)."
          : "A new plan is created inactive — activate it from the list when it is ready to sell."
      }
      size="lg"
    >
      <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
        {submitError ? <Alert>{submitError}</Alert> : null}

        <FormGrid columns={2}>
          <SearchableSelect
            label="Category"
            required
            placeholder="Search categories…"
            options={categoryOptions}
            value={form.watch("categoryId")}
            onChange={(value) => form.setValue("categoryId", value, { shouldValidate: true })}
            error={form.formState.errors.categoryId?.message}
          />
          <Field
            label="Name"
            required
            error={form.formState.errors.name?.message}
            {...form.register("name")}
          />
          <Field
            label="Price"
            type="number"
            step="0.01"
            min={0.01}
            leading="₹"
            error={form.formState.errors.price?.message}
            {...form.register("price", { valueAsNumber: true })}
          />
          <Field
            label="Term (months)"
            type="number"
            min={1}
            max={60}
            error={form.formState.errors.termMonths?.message}
            {...form.register("termMonths", { valueAsNumber: true })}
          />
          <Field
            label="Visits included"
            type="number"
            min={1}
            max={52}
            error={form.formState.errors.visitsIncluded?.message}
            {...form.register("visitsIncluded", { valueAsNumber: true })}
          />
        </FormGrid>

        <Textarea
          label="Description"
          rows={3}
          hint="Shown to customers browsing AMC plans."
          error={form.formState.errors.description?.message}
          {...form.register("description")}
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
