"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation } from "@tanstack/react-query";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { RequireAuth } from "@/components/RequireAuth";
import { Alert, Button, Card, Field, PageHeading } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import {
  BOOKING_REQUIRED_CATEGORIES,
  SUPPORT_TICKET_CATEGORY_OPTIONS,
  SUPPORT_TICKET_PRIORITY_OPTIONS,
} from "@/lib/support";
import { SupportTicketCategory } from "@/lib/types";
import type { SupportTicketDetailResponse } from "@/lib/types";

/**
 * Raise a new support ticket (SRS 11.18.1-2, task 92). Wrapped in Suspense:
 * useSearchParams opts the tree below it out of static rendering (see
 * booking/summary/page.tsx for the same pattern).
 */
export default function NewSupportTicketPage() {
  return (
    <Suspense fallback={<main className="mx-auto w-full max-w-2xl px-6 py-12" />}>
      <RequireAuth>
        <NewSupportTicketScreen />
      </RequireAuth>
    </Suspense>
  );
}

const selectClassName =
  "rounded-lg border border-black/15 bg-transparent px-3 py-2 text-sm outline-none focus:border-black focus:ring-1 focus:ring-black dark:border-white/20 dark:focus:border-white dark:focus:ring-white";

const createTicketSchema = z
  .object({
    category: z.coerce.number().int().min(0).max(7),
    bookingId: z.string().max(64).optional(),
    subject: z.string().min(1, "Subject is required").max(200, "Subject must be 200 characters or fewer."),
    description: z
      .string()
      .min(1, "Description is required")
      .max(4000, "Description must be 4000 characters or fewer."),
    priority: z.string().optional(),
  })
  .refine(
    (values) => !BOOKING_REQUIRED_CATEGORIES.has(values.category as SupportTicketCategory) || !!values.bookingId?.trim(),
    { message: "A booking reference is required for this category.", path: ["bookingId"] },
  );

type CreateTicketFormValues = z.input<typeof createTicketSchema>;
type CreateTicketPayload = z.output<typeof createTicketSchema>;

function NewSupportTicketScreen() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const prefilledBookingId = searchParams.get("bookingId") ?? "";

  const form = useForm<CreateTicketFormValues, unknown, CreateTicketPayload>({
    resolver: zodResolver(createTicketSchema),
    defaultValues: {
      category: prefilledBookingId ? SupportTicketCategory.BookingIssue : SupportTicketCategory.GeneralInquiry,
      bookingId: prefilledBookingId,
      subject: "",
      description: "",
      priority: "",
    },
  });
  const { errors } = form.formState;
  const selectedCategory = Number(form.watch("category")) as SupportTicketCategory;
  const bookingRequired = BOOKING_REQUIRED_CATEGORIES.has(selectedCategory);

  const createMutation = useMutation({
    mutationFn: (values: CreateTicketPayload) =>
      apiFetch<SupportTicketDetailResponse>(`${API_V1}/support-tickets`, {
        method: "POST",
        authenticated: true,
        body: JSON.stringify({
          category: values.category,
          bookingId: values.bookingId?.trim() ? values.bookingId.trim() : null,
          subject: values.subject,
          description: values.description,
          priority: values.priority !== "" && values.priority !== undefined ? Number(values.priority) : null,
        }),
      }),
    onSuccess: (ticket) => {
      router.push(`/support/${ticket.id}`);
    },
  });

  return (
    <main className="mx-auto w-full max-w-2xl px-6 py-12">
      <PageHeading title="Raise an issue" subtitle="Tell us what's wrong and we'll take a look." />

      <Card title="New ticket">
        <form
          onSubmit={form.handleSubmit((values) => createMutation.mutate(values))}
          className="flex flex-col gap-4"
          noValidate
        >
          {createMutation.isError ? <Alert>{describeError(createMutation.error)}</Alert> : null}

          <div className="flex flex-col gap-1.5">
            <label htmlFor="ticket-category" className="text-sm font-medium">
              Category
            </label>
            <select id="ticket-category" className={selectClassName} {...form.register("category")}>
              {SUPPORT_TICKET_CATEGORY_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </div>

          <Field
            label={bookingRequired ? "Booking reference (required)" : "Booking reference (optional)"}
            placeholder="Booking ID"
            error={errors.bookingId?.message}
            {...form.register("bookingId")}
          />

          <Field label="Subject" error={errors.subject?.message} {...form.register("subject")} />

          <div className="flex flex-col gap-1.5">
            <label htmlFor="ticket-description" className="text-sm font-medium">
              Description
            </label>
            <textarea
              id="ticket-description"
              rows={5}
              maxLength={4000}
              aria-invalid={errors.description ? true : undefined}
              aria-describedby={errors.description ? "ticket-description-error" : undefined}
              className={selectClassName}
              {...form.register("description")}
            />
            {errors.description ? (
              <p id="ticket-description-error" className="text-xs text-red-600 dark:text-red-400">
                {errors.description.message}
              </p>
            ) : null}
          </div>

          <div className="flex flex-col gap-1.5">
            <label htmlFor="ticket-priority" className="text-sm font-medium">
              Priority (optional)
            </label>
            <select id="ticket-priority" className={selectClassName} {...form.register("priority")}>
              <option value="">Default</option>
              {SUPPORT_TICKET_PRIORITY_OPTIONS.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </div>

          <Button type="submit" disabled={createMutation.isPending}>
            {createMutation.isPending ? "Submitting…" : "Submit ticket"}
          </Button>
        </form>
      </Card>
    </main>
  );
}
