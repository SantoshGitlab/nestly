"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useParams } from "next/navigation";
import { useState } from "react";
import { RequireAuth } from "@/components/RequireAuth";
import { Alert, Button, Card, PageHeading } from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import { authorTypeLabel, categoryLabel, priorityLabel, statusLabel } from "@/lib/support";
import { SupportTicketCommentAuthorType } from "@/lib/types";
import type { SupportTicketDetailResponse } from "@/lib/types";

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
  const queryClient = useQueryClient();
  const [comment, setComment] = useState("");
  const [commentError, setCommentError] = useState<string | null>(null);

  const query = useQuery({
    queryKey: ["support-ticket", id],
    queryFn: () =>
      apiFetch<SupportTicketDetailResponse>(`${API_V1}/support-tickets/${id}`, { authenticated: true }),
  });

  const commentMutation = useMutation({
    mutationFn: (text: string) =>
      apiFetch<SupportTicketDetailResponse>(`${API_V1}/support-tickets/${id}/comments`, {
        method: "POST",
        authenticated: true,
        body: JSON.stringify({ comment: text }),
      }),
    onSuccess: (updated) => {
      queryClient.setQueryData(["support-ticket", id], updated);
      setComment("");
    },
  });

  const submitComment = () => {
    const trimmed = comment.trim();
    if (!trimmed) {
      setCommentError("Enter a reply before sending.");
      return;
    }
    if (trimmed.length > 2000) {
      setCommentError("Reply must be 2000 characters or fewer.");
      return;
    }
    setCommentError(null);
    commentMutation.mutate(trimmed);
  };

  if (query.isPending) {
    return <main className="mx-auto w-full max-w-3xl px-6 py-12 text-sm text-neutral-500">Loading…</main>;
  }

  if (query.isError || !query.data) {
    return (
      <main className="mx-auto w-full max-w-3xl px-6 py-12">
        <Alert>{describeError(query.error)}</Alert>
      </main>
    );
  }

  const ticket = query.data;

  return (
    <main className="mx-auto w-full max-w-3xl px-6 py-12">
      <PageHeading title={ticket.subject} subtitle={`Ticket ID: ${ticket.id}`} />

      <div className="flex flex-col gap-6">
        <Card title="Details">
          <dl className="flex flex-col gap-2 text-sm">
            <div className="flex items-center justify-between">
              <dt className="text-neutral-600 dark:text-neutral-400">Category</dt>
              <dd className="font-medium">{categoryLabel(ticket.category)}</dd>
            </div>
            <div className="flex items-center justify-between">
              <dt className="text-neutral-600 dark:text-neutral-400">Priority</dt>
              <dd className="font-medium">{priorityLabel(ticket.priority)}</dd>
            </div>
            <div className="flex items-center justify-between">
              <dt className="text-neutral-600 dark:text-neutral-400">Status</dt>
              <dd className="font-medium">{statusLabel(ticket.status)}</dd>
            </div>
            {ticket.bookingId ? (
              <div className="flex items-center justify-between">
                <dt className="text-neutral-600 dark:text-neutral-400">Booking</dt>
                <dd className="font-medium">
                  <Link href={`/bookings/${ticket.bookingId}`} className="hover:underline">
                    View booking
                  </Link>
                </dd>
              </div>
            ) : null}
          </dl>
          <p className="mt-4 text-sm text-neutral-700 dark:text-neutral-300">{ticket.description}</p>
          {ticket.resolutionSummary ? (
            <div className="mt-4 border-t border-black/10 pt-4 text-sm dark:border-white/15">
              <p className="mb-1 font-medium">Resolution</p>
              <p className="text-neutral-700 dark:text-neutral-300">{ticket.resolutionSummary}</p>
            </div>
          ) : null}
        </Card>

        <Card title="Conversation">
          {ticket.comments.length === 0 ? (
            <p className="text-sm text-neutral-500">No replies yet.</p>
          ) : (
            <ul className="flex flex-col gap-3">
              {ticket.comments.map((c) => (
                <li
                  key={c.id}
                  className={`rounded-lg border p-3 text-sm ${
                    c.authorType === SupportTicketCommentAuthorType.Customer
                      ? "border-black/15 dark:border-white/20"
                      : "border-blue-200 bg-blue-50 dark:border-blue-900 dark:bg-blue-950"
                  }`}
                >
                  <div className="mb-1 flex items-center justify-between text-xs text-neutral-500">
                    <span className="font-medium">{authorTypeLabel(c.authorType)}</span>
                    <span>{new Date(c.createdAt).toLocaleString()}</span>
                  </div>
                  <p>{c.comment}</p>
                </li>
              ))}
            </ul>
          )}

          <div className="mt-5 flex flex-col gap-2 border-t border-black/10 pt-4 dark:border-white/15">
            {commentMutation.isError ? <Alert>{describeError(commentMutation.error)}</Alert> : null}
            <label htmlFor="ticket-reply" className="text-sm font-medium">
              Reply
            </label>
            <textarea
              id="ticket-reply"
              rows={3}
              maxLength={2000}
              value={comment}
              onChange={(e) => setComment(e.target.value)}
              aria-invalid={commentError ? true : undefined}
              aria-describedby={commentError ? "ticket-reply-error" : undefined}
              className="rounded-lg border border-black/15 bg-transparent px-3 py-2 text-sm outline-none focus:border-black focus:ring-1 focus:ring-black dark:border-white/20 dark:focus:border-white dark:focus:ring-white"
            />
            {commentError ? (
              <p id="ticket-reply-error" className="text-xs text-red-600 dark:text-red-400">
                {commentError}
              </p>
            ) : null}
            <Button type="button" onClick={submitComment} disabled={commentMutation.isPending} className="self-start">
              {commentMutation.isPending ? "Sending…" : "Send reply"}
            </Button>
          </div>
        </Card>

        <Link href="/support" className="text-sm font-medium hover:underline">
          Back to tickets
        </Link>
      </div>
    </main>
  );
}
