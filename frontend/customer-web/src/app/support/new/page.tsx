"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation } from "@tanstack/react-query";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense, useEffect, useRef } from "react";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { ScreenSkeleton } from "@/components/patterns";
import { RequireAuth } from "@/components/RequireAuth";
import { Alert, Button, Card, Field, PageHeading, Select, Textarea } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import {
  BOOKING_REQUIRED_CATEGORIES,
  SUPPORT_TICKET_CATEGORY_OPTIONS,
  SUPPORT_TICKET_PRIORITY_OPTIONS,
} from "@/lib/support";
import { SupportTicketCategory } from "@/lib/types";
import type { SupportTicketDetailResponse } from "@/lib/types";

const SUBJECT_MAX = 200;
const DESCRIPTION_MAX = 4000;

/**
 * Draft persistence (task 228). The category/subject/description/priority
 * fields are the only state this form has, and the booking-reference hint
 * below explicitly sends the customer to a booking's detail page to look up
 * the ID they need - the most ordinary way to fill this in is to navigate
 * away and come back, which used to wipe everything they had already typed,
 * description included. sessionStorage rather than the booking-summary
 * page's per-record key: this is a create form with no id to key off.
 */
const DRAFT_KEY = "nestly.support-ticket-draft";

/**
 * Both selects render the enum's numeric value, which the DOM hands back as a
 * string. Deriving the allowed set from the option lists rather than writing
 * `min(0).max(7)` means adding a category to `lib/support.ts` cannot leave a
 * validation range silently behind.
 */
const CATEGORY_VALUES = SUPPORT_TICKET_CATEGORY_OPTIONS.map((option) => String(option.value));
const PRIORITY_VALUES = SUPPORT_TICKET_PRIORITY_OPTIONS.map((option) => String(option.value));

const CATEGORY_SELECT_OPTIONS = SUPPORT_TICKET_CATEGORY_OPTIONS.map((option) => ({
  value: String(option.value),
  label: option.label,
}));
const PRIORITY_SELECT_OPTIONS = [
  { value: "", label: "Let support decide" },
  ...SUPPORT_TICKET_PRIORITY_OPTIONS.map((option) => ({
    value: String(option.value),
    label: option.label,
  })),
];

const createTicketSchema = z
  .object({
    category: z.string().refine((value) => CATEGORY_VALUES.includes(value), "Choose a category"),
    bookingId: z.string().max(64, "Booking reference must be 64 characters or fewer"),
    subject: z
      .string()
      .trim()
      .min(1, "Subject is required")
      .max(SUBJECT_MAX, `Subject must be ${SUBJECT_MAX} characters or fewer`),
    description: z
      .string()
      .trim()
      .min(1, "Description is required")
      .max(DESCRIPTION_MAX, `Description must be ${DESCRIPTION_MAX} characters or fewer`),
    priority: z
      .string()
      .refine((value) => value === "" || PRIORITY_VALUES.includes(value), "Choose a priority"),
  })
  // Mirrors CreateSupportTicketRequestValidator's server-side rule so the
  // customer is told before the round trip, and on the control that is
  // actually missing.
  .refine(
    (values) =>
      !BOOKING_REQUIRED_CATEGORIES.has(Number(values.category) as SupportTicketCategory) ||
      values.bookingId.trim() !== "",
    { message: "A booking reference is required for this category.", path: ["bookingId"] },
  );

type CreateTicketFormValues = z.infer<typeof createTicketSchema>;

/**
 * Raise a new support ticket (SRS 11.18.1-2, task 92). Wrapped in Suspense:
 * useSearchParams opts the tree below it out of static rendering (see
 * booking/summary/page.tsx for the same pattern).
 */
export default function NewSupportTicketPage() {
  return (
    // A skeleton rather than an empty <main>: the fallback used to render a
    // blank page the exact height of nothing, so the form arrived as a jump.
    <Suspense
      fallback={<ScreenSkeleton cards={1} className="mx-auto w-full max-w-2xl px-4 py-8 sm:px-6 sm:py-12" />}
    >
      <RequireAuth>
        <NewSupportTicketScreen />
      </RequireAuth>
    </Suspense>
  );
}

function NewSupportTicketScreen() {
  const router = useRouter();
  const prefilledBookingId = useSearchParams().get("bookingId") ?? "";

  const form = useForm<CreateTicketFormValues>({
    resolver: zodResolver(createTicketSchema),
    defaultValues: {
      // Arriving from a booking's "Raise an issue" link is a booking issue
      // until the customer says otherwise.
      category: String(
        prefilledBookingId ? SupportTicketCategory.BookingIssue : SupportTicketCategory.GeneralInquiry,
      ),
      bookingId: prefilledBookingId,
      subject: "",
      description: "",
      priority: "",
    },
  });

  const { errors } = form.formState;
  const bookingRequired = BOOKING_REQUIRED_CATEGORIES.has(
    Number(form.watch("category")) as SupportTicketCategory,
  );
  const descriptionLength = form.watch("description").length;

  // Restore a draft left by a detour off this page (see DRAFT_KEY above).
  // Skipped when arriving via a booking's own "Raise an issue" link -
  // `prefilledBookingId` being set means this is a deliberate fresh start
  // for that specific booking, not a return trip.
  useEffect(() => {
    if (prefilledBookingId) return;
    try {
      const raw = sessionStorage.getItem(DRAFT_KEY);
      if (raw) form.reset(JSON.parse(raw) as CreateTicketFormValues);
    } catch {
      // Corrupt or unavailable storage - the blank form is still usable.
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Persist on every change so a detour to look up a booking reference
  // doesn't cost the customer what they already wrote.
  useEffect(() => {
    const subscription = form.watch((values) => {
      try {
        sessionStorage.setItem(DRAFT_KEY, JSON.stringify(values));
      } catch {
        // Storage can be unavailable (private mode); nothing to fall back to.
      }
    });
    return () => subscription.unsubscribe();
  }, [form]);

  /**
   * The submit button disables itself while the request is in flight, but a
   * second click can be dispatched in the same tick as the first, before that
   * state has rendered — and the cost of losing that race here is a duplicate
   * ticket in the support queue.
   */
  const inFlight = useRef(false);

  const createMutation = useMutation({
    mutationFn: (values: CreateTicketFormValues) =>
      apiFetch<SupportTicketDetailResponse>(`${API_V1}/support-tickets`, {
        method: "POST",
        authenticated: true,
        body: JSON.stringify({
          category: Number(values.category),
          // The column is nullable; a blank string is not "no booking".
          bookingId: values.bookingId.trim() === "" ? null : values.bookingId.trim(),
          subject: values.subject.trim(),
          description: values.description.trim(),
          priority: values.priority === "" ? null : Number(values.priority),
        }),
      }),
    onSuccess: (ticket) => {
      // The ticket exists now; a leftover draft would otherwise resurrect
      // this exact subject/description on the next visit to this form.
      try {
        sessionStorage.removeItem(DRAFT_KEY);
      } catch {
        // Not worth surfacing - the ticket was created either way.
      }
      // Deliberately left latched: the route transition is asynchronous, and
      // releasing the guard here would reopen the double-submit window while
      // the next screen loads.
      router.push(`/support/${ticket.id}`);
    },
    onError: () => {
      inFlight.current = false;
    },
  });

  const onSubmit = form.handleSubmit((values) => {
    if (inFlight.current) return;
    inFlight.current = true;
    createMutation.mutate(values);
  });

  return (
    <main className="mx-auto w-full max-w-2xl animate-rise px-4 py-8 sm:px-6 sm:py-12">
      <PageHeading title="Raise an issue" subtitle="Tell us what's wrong and we'll take a look." />

      <Card title="New ticket">
        <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
          {createMutation.isError ? (
            <Alert tone="error" title="Couldn't create your ticket">
              {describeError(createMutation.error)}
            </Alert>
          ) : null}

          <Select
            label="Category"
            options={CATEGORY_SELECT_OPTIONS}
            error={errors.category?.message}
            {...form.register("category")}
          />

          <Field
            label="Booking reference"
            placeholder="Booking ID"
            required={bookingRequired}
            hint={
              bookingRequired
                ? "Required for this category — you'll find it on the booking's detail page."
                : "Optional. Add it if this is about a specific booking."
            }
            error={errors.bookingId?.message}
            {...form.register("bookingId")}
          />

          <Field
            label="Subject"
            required
            maxLength={SUBJECT_MAX}
            error={errors.subject?.message}
            {...form.register("subject")}
          />

          <Textarea
            label="Description"
            rows={6}
            required
            maxLength={DESCRIPTION_MAX}
            hint={`${descriptionLength} of ${DESCRIPTION_MAX} characters`}
            error={errors.description?.message}
            {...form.register("description")}
          />

          <Select
            label="Priority"
            options={PRIORITY_SELECT_OPTIONS}
            hint="Support may adjust this after reading your ticket."
            error={errors.priority?.message}
            {...form.register("priority")}
          />

          <Button type="submit" className="self-start" loading={createMutation.isPending}>
            Submit ticket
          </Button>
        </form>
      </Card>
    </main>
  );
}
