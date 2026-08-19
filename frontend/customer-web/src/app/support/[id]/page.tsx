"use client";

import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useForm } from "react-hook-form";
import { z } from "zod";
import { ChatWidget } from "@/components/ChatWidget";
import {
  BannerBreadcrumb,
  DetailList,
  DetailRow,
  ScreenSkeleton,
  formatInstant,
  supportStatusTone,
} from "@/components/patterns";
import { PageBanner } from "@/components/PageBanner";
import { RequireAuth } from "@/components/RequireAuth";
import {
  Alert,
  Badge,
  Button,
  Card,
  Divider,
  Textarea,
  cx,
} from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import { authorTypeLabel, categoryLabel, priorityLabel, statusLabel } from "@/lib/support";
import {
  ChatContextType,
  DisputeResolutionOutcome,
  SupportTicketCommentAuthorType,
  SupportTicketStatus,
} from "@/lib/types";
import type { SupportTicketCommentResponse, SupportTicketDetailResponse } from "@/lib/types";

const REPLY_MAX = 2000;

/** Support ticket detail: thread + reply (SRS 11.18.3, task 92). */
export default function SupportTicketDetailPage() {
  return (
    <RequireAuth>
      <SupportTicketDetailScreen />
    </RequireAuth>
  );
}

function SupportTicketDetailScreen() {
  const { id } = useParams<{ id: string }>();

  const query = useQuery({
    queryKey: ["support-ticket", id],
    queryFn: () =>
      apiFetch<SupportTicketDetailResponse>(`${API_V1}/support-tickets/${id}`, {
        authenticated: true,
      }),
  });

  if (query.isPending) {
    return (
      <main className="flex w-full flex-col" aria-hidden>
        <div className="listing-banner h-[13.5rem] w-full sm:h-[15.5rem]" />
        <ScreenSkeleton cards={3} className="mx-auto w-full max-w-7xl px-4 py-10 sm:px-6 sm:py-14" />
      </main>
    );
  }

  if (query.isError) {
    return (
      <main className="mx-auto w-full max-w-7xl px-4 py-12 sm:px-6">
        <Alert
          tone="error"
          title="Couldn't load this ticket"
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

  const ticket = query.data;

  return (
    <main className="flex w-full flex-col animate-rise">
      <PageBanner
        title={ticket.subject}
        description={`Ticket ID: ${ticket.id}`}
        badge={<Badge tone={supportStatusTone(ticket.status)}>{statusLabel(ticket.status)}</Badge>}
        breadcrumb={
          <BannerBreadcrumb
            items={[{ label: "Home", href: "/" }, { label: "Support tickets", href: "/support" }, { label: ticket.subject }]}
          />
        }
      />

      <div className="mx-auto w-full max-w-7xl px-4 py-10 sm:px-6 sm:py-14">
      <div className="flex flex-col gap-6">
        {ticket.resolutionSummary ? (
          <Alert tone="success" title="Resolution">
            {ticket.resolutionSummary}
          </Alert>
        ) : null}

        {/* `isDisputed` and its outcome were carried on the response and never
            rendered, so a customer whose refund claim had been reviewed had no
            way to see the decision anywhere in the app. */}
        {ticket.isDisputed ? <DisputeAlert outcome={ticket.disputeOutcome} /> : null}

        <Card title="Details">
          <DetailList>
            <DetailRow label="Category">{categoryLabel(ticket.category)}</DetailRow>
            <DetailRow label="Priority">{priorityLabel(ticket.priority)}</DetailRow>
            <DetailRow label="Status">
              <Badge tone={supportStatusTone(ticket.status)}>{statusLabel(ticket.status)}</Badge>
            </DetailRow>
            <DetailRow label="Raised" numeric>
              {formatInstant(ticket.createdAtUtc)}
            </DetailRow>
            <DetailRow label="Last updated" numeric>
              {formatInstant(ticket.updatedAtUtc)}
            </DetailRow>
            {ticket.bookingId ? (
              <DetailRow label="Booking">
                <Link
                  href={`/bookings/${ticket.bookingId}`}
                  className="text-brand-600 underline-offset-4 hover:underline dark:text-brand-400"
                >
                  View booking
                </Link>
              </DetailRow>
            ) : null}
          </DetailList>

          <Divider className="my-5" />

          <p className="whitespace-pre-wrap text-sm leading-relaxed text-fg">{ticket.description}</p>
        </Card>

        <Card title="Conversation">
          <Thread comments={ticket.comments} />
          <Divider className="my-5" />
          <ReplyForm ticketId={ticket.id} status={ticket.status} />
        </Card>

        <ChatWidget contextType={ChatContextType.SupportTicket} contextId={ticket.id} />

        <Link
          href="/support"
          className="text-sm font-medium text-brand-600 underline-offset-4 hover:underline dark:text-brand-400"
        >
          Back to tickets
        </Link>
      </div>
      </div>
    </main>
  );
}

function DisputeAlert({ outcome }: { outcome: DisputeResolutionOutcome | null }) {
  if (outcome === null) {
    return (
      <Alert tone="info" title="Under dispute review">
        We&apos;re reviewing this ticket. You&apos;ll see the decision here once it&apos;s made.
      </Alert>
    );
  }

  return outcome === DisputeResolutionOutcome.RefundValid ? (
    <Alert tone="success" title="Dispute upheld">
      We agreed with your claim. Any refund due is on its way — check your wallet or original payment
      method.
    </Alert>
  ) : (
    <Alert tone="warning" title="Dispute closed">
      After reviewing, we weren&apos;t able to uphold this claim. Reply below if you have anything
      further to add.
    </Alert>
  );
}

/* -------------------------------------------------------------------------- */
/* Thread                                                                     */
/* -------------------------------------------------------------------------- */

function Thread({ comments }: { comments: SupportTicketCommentResponse[] }) {
  if (comments.length === 0) {
    return (
      <p className="text-sm text-fg-muted">
        No replies yet. We usually respond within one working day.
      </p>
    );
  }

  return (
    <ol className="flex list-none flex-col gap-3">
      {comments.map((comment) => {
        const isCustomer = comment.authorType === SupportTicketCommentAuthorType.Customer;
        return (
          <li
            key={comment.id}
            className={cx(
              "rounded-xl border px-4 py-3",
              isCustomer
                ? "border-line bg-surface-2"
                : "border-brand-600/20 bg-brand-50 dark:bg-brand-500/10",
            )}
          >
            <div className="mb-1.5 flex flex-wrap items-baseline justify-between gap-2">
              {/* The author is named in text, so the side the bubble is tinted
                  is reinforcement rather than the only signal. */}
              <span className="text-xs font-semibold text-fg">
                {authorTypeLabel(comment.authorType)}
              </span>
              <span className="nums text-xs text-fg-subtle">{formatInstant(comment.createdAt)}</span>
            </div>
            <p className="whitespace-pre-wrap text-sm leading-relaxed text-fg">{comment.comment}</p>
          </li>
        );
      })}
    </ol>
  );
}

/* -------------------------------------------------------------------------- */
/* Reply                                                                      */
/* -------------------------------------------------------------------------- */

const replySchema = z.object({
  comment: z
    .string()
    .trim()
    .min(1, "Enter a reply before sending")
    .max(REPLY_MAX, `Reply must be ${REPLY_MAX} characters or fewer`),
});

type ReplyFormValues = z.infer<typeof replySchema>;

function ReplyForm({ ticketId, status }: { ticketId: string; status: SupportTicketStatus }) {
  const queryClient = useQueryClient();

  const form = useForm<ReplyFormValues>({
    resolver: zodResolver(replySchema),
    defaultValues: { comment: "" },
  });

  const mutation = useMutation({
    mutationFn: (comment: string) =>
      apiFetch<SupportTicketDetailResponse>(`${API_V1}/support-tickets/${ticketId}/comments`, {
        method: "POST",
        authenticated: true,
        body: JSON.stringify({ comment }),
      }),
    onSuccess: (updated) => {
      // The endpoint answers with the whole ticket, so seeding the cache with
      // it shows the new reply without a second round trip.
      queryClient.setQueryData(["support-ticket", ticketId], updated);
      form.reset({ comment: "" });
    },
  });

  return (
    <form
      onSubmit={form.handleSubmit((values) => {
        // `isPending` already disables the button; this drops a click that
        // lands in the same tick, before that disable has rendered.
        if (mutation.isPending) return;
        mutation.mutate(values.comment.trim());
      })}
      className="flex flex-col gap-3"
      noValidate
    >
      {mutation.isError ? (
        <Alert tone="error" title="Couldn't send your reply">
          {describeError(mutation.error)}
        </Alert>
      ) : null}

      {/* The server accepts comments on a closed ticket, so the box stays
          usable — but saying nothing would leave a customer replying into what
          they reasonably assume is a live conversation. */}
      {status === SupportTicketStatus.Closed ? (
        <Alert tone="info">
          This ticket is closed. You can still reply, but a new ticket will reach us faster.
        </Alert>
      ) : null}

      <Textarea
        label="Reply"
        rows={4}
        maxLength={REPLY_MAX}
        // The error belongs on the control, not in a detached banner above the
        // form — `Textarea` wires it to `aria-describedby` and `aria-invalid`.
        error={form.formState.errors.comment?.message}
        {...form.register("comment")}
      />

      <Button type="submit" className="self-start" loading={mutation.isPending}>
        Send reply
      </Button>
    </form>
  );
}
