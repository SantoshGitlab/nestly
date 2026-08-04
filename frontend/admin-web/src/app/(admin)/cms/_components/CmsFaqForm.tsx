"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { Alert, Button, Field, Select, Textarea } from "@/components/ui";
import { FormActions, FormGrid } from "@/components/data-table";
import { CmsPlacement, type CmsFaqCreateRequest, type CmsFaqResponse, type CmsFaqUpdateRequest } from "@/lib/cms-types";
import { PLACEMENT_OPTIONS, datetimeLocalToUtc, utcToDatetimeLocal } from "./cmsDisplay";

/**
 * Create/edit form for a site-level FAQ entry (SRS 12.16.1, task 125c):
 * question, answer, placement, sort order, and an optional publish window
 * (task 124d/124f). One component serves both modes - `faq` present means
 * "edit". New FAQs always start as Draft; the manage screen's
 * publish/unpublish toggle handles the status transition afterwards (see
 * CmsFaqsTable).
 */

const faqFormSchema = z
  .object({
    question: z.string().min(1, "Question is required").max(300, "Question must be 300 characters or fewer"),
    answer: z.string().min(1, "Answer is required"),
    placement: z.number().int(),
    sortOrder: z.number().int().min(0, "Sort order cannot be negative"),
    publishStart: z.string(),
    publishEnd: z.string(),
  })
  .refine((values) => values.publishStart === "" || values.publishEnd === "" || values.publishEnd > values.publishStart, {
    path: ["publishEnd"],
    message: "Publish end must be after publish start.",
  });

type FaqFormValues = z.infer<typeof faqFormSchema>;

function defaultValuesFor(faq: CmsFaqResponse | null): FaqFormValues {
  if (!faq) {
    return {
      question: "",
      answer: "",
      placement: CmsPlacement.General,
      sortOrder: 0,
      publishStart: "",
      publishEnd: "",
    };
  }

  return {
    question: faq.question,
    answer: faq.answer,
    placement: faq.placement,
    sortOrder: faq.sortOrder,
    publishStart: utcToDatetimeLocal(faq.publishStartUtc),
    publishEnd: utcToDatetimeLocal(faq.publishEndUtc),
  };
}

export function CmsFaqForm({
  faq,
  isSubmitting,
  submitError,
  onSubmit,
  onCancel,
}: {
  /** Present in edit mode; null when creating a new FAQ. */
  faq: CmsFaqResponse | null;
  isSubmitting: boolean;
  submitError: string | null;
  onSubmit: (request: CmsFaqCreateRequest | CmsFaqUpdateRequest) => void;
  onCancel?: () => void;
}) {
  const form = useForm<FaqFormValues>({
    resolver: zodResolver(faqFormSchema),
    defaultValues: defaultValuesFor(faq),
  });

  // Re-seed the form whenever the FAQ being edited changes - react-hook-form
  // only applies `defaultValues` once, at mount (same reasoning as
  // CouponForm's identical effect).
  useEffect(() => {
    form.reset(defaultValuesFor(faq));
  }, [faq, form]);

  const submit = form.handleSubmit((values) => {
    onSubmit({
      question: values.question.trim(),
      answer: values.answer.trim(),
      placement: values.placement as CmsPlacement,
      sortOrder: values.sortOrder,
      publishStartUtc: datetimeLocalToUtc(values.publishStart),
      publishEndUtc: datetimeLocalToUtc(values.publishEnd),
    });
  });

  return (
    <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
      {submitError ? <Alert>{submitError}</Alert> : null}

      <Field label="Question" required error={form.formState.errors.question?.message} {...form.register("question")} />

      <Textarea
        label="Answer"
        required
        rows={6}
        error={form.formState.errors.answer?.message}
        {...form.register("answer")}
      />

      <FormGrid>
        <Select
          label="Placement"
          error={form.formState.errors.placement?.message}
          options={PLACEMENT_OPTIONS}
          {...form.register("placement", { valueAsNumber: true })}
        />
        <Field
          label="Sort order"
          type="number"
          min={0}
          hint="Lower numbers appear first."
          error={form.formState.errors.sortOrder?.message}
          {...form.register("sortOrder", { valueAsNumber: true })}
        />
      </FormGrid>

      <FormGrid>
        <Field
          label="Publish from"
          type="datetime-local"
          hint="Optional — your local time."
          error={form.formState.errors.publishStart?.message}
          {...form.register("publishStart")}
        />
        <Field
          label="Publish until"
          type="datetime-local"
          hint="Optional — your local time."
          error={form.formState.errors.publishEnd?.message}
          {...form.register("publishEnd")}
        />
      </FormGrid>

      <FormActions align="start">
        <Button type="submit" loading={isSubmitting}>
          {faq ? "Save changes" : "Create FAQ"}
        </Button>
        {onCancel ? (
          <Button type="button" variant="secondary" onClick={onCancel} disabled={isSubmitting}>
            Cancel
          </Button>
        ) : null}
      </FormActions>
    </form>
  );
}
