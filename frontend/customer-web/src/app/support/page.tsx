"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { formatInstant, supportStatusTone } from "@/components/patterns";
import { RequireAuth } from "@/components/RequireAuth";
import {
  Alert,
  Badge,
  Button,
  EmptyState,
  PageHeading,
  Skeleton,
} from "@/components/ui";
import { API_V1, apiFetch, describeError } from "@/lib/api";
import { categoryLabel, statusLabel } from "@/lib/support";
import type { SupportTicketSummaryResponse } from "@/lib/types";

/** Support ticket list: the caller's own tickets, newest first (SRS 11.18, task 92). */
export default function SupportTicketsPage() {
  return (
    <RequireAuth>
      <SupportTicketsScreen />
    </RequireAuth>
  );
}

function SupportTicketsScreen() {
  const query = useQuery({
    queryKey: ["support-tickets"],
    queryFn: () =>
      apiFetch<SupportTicketSummaryResponse[]>(`${API_V1}/support-tickets`, { authenticated: true }),
  });

  return (
    <main className="mx-auto w-full max-w-3xl px-4 py-8 sm:px-6 sm:py-12">
      <PageHeading
        title="Support tickets"
        subtitle="Everything you've raised with us, and where each one stands."
        // A styled Link, not `<Link><Button/></Link>`: a button inside an
        // anchor is invalid HTML and exposes two nested controls for one
        // action, which is the exact shape the booking detail screen fixed.
        actions={<RaiseIssueLink />}
      />

      {query.isPending ? (
        <ul className="flex list-none flex-col gap-3" aria-hidden>
          {[0, 1, 2].map((row) => (
            <li key={row}>
              {/* Sized to the real row below so the list does not reflow when
                  the tickets land. */}
              <Skeleton className="h-[5.75rem] rounded-2xl" />
            </li>
          ))}
        </ul>
      ) : query.isError ? (
        <Alert
          tone="error"
          title="Couldn't load your tickets"
          action={
            <Button size="sm" variant="secondary" onClick={() => query.refetch()}>
              Retry
            </Button>
          }
        >
          {describeError(query.error)}
        </Alert>
      ) : query.data.length === 0 ? (
        <EmptyState
          icon={<TicketIcon />}
          title="No tickets yet"
          description="If something's wrong with a booking or your account, raise an issue and we'll pick it up."
          action={<RaiseIssueLink />}
        />
      ) : (
        <ul className="flex list-none flex-col gap-3">
          {query.data.map((ticket) => (
            <li key={ticket.id}>
              <TicketRow ticket={ticket} />
            </li>
          ))}
        </ul>
      )}
    </main>
  );
}

/** One accessible name, one styling, two placements — the heading action and
 *  the empty state's next step. */
function RaiseIssueLink() {
  return (
    <Link
      href="/support/new"
      className="inline-flex h-10 items-center justify-center rounded-lg bg-brand-600 px-4 text-sm font-medium text-fg-on-brand shadow-brand transition duration-fast ease-out hover:bg-brand-700 active:scale-[0.98]"
    >
      Raise an issue
    </Link>
  );
}

function TicketRow({ ticket }: { ticket: SupportTicketSummaryResponse }) {
  return (
    <Link
      href={`/support/${ticket.id}`}
      className="block rounded-2xl border border-line bg-surface p-4 shadow-sm transition duration-fast ease-out hover:border-line-strong hover:bg-surface-2"
    >
      <div className="flex items-start justify-between gap-3">
        <span className="min-w-0 font-medium text-fg">{ticket.subject}</span>
        <Badge tone={supportStatusTone(ticket.status)} className="shrink-0">
          {statusLabel(ticket.status)}
        </Badge>
      </div>
      <p className="mt-1.5 text-sm text-fg-muted">{categoryLabel(ticket.category)}</p>
      <p className="nums mt-1 text-xs text-fg-subtle">
        Raised {formatInstant(ticket.createdAtUtc)}
        {/* The two timestamps are equal on a brand-new ticket; showing both
            then reads as a bug rather than as an update. */}
        {ticket.updatedAtUtc !== ticket.createdAtUtc
          ? ` · Updated ${formatInstant(ticket.updatedAtUtc)}`
          : ""}
      </p>
    </Link>
  );
}

function TicketIcon() {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.75"
      strokeLinecap="round"
      strokeLinejoin="round"
      className="h-5 w-5"
      aria-hidden
    >
      <path d="M21 8V6a1 1 0 0 0-1-1H4a1 1 0 0 0-1 1v2a2 2 0 0 1 0 8v2a1 1 0 0 0 1 1h16a1 1 0 0 0 1-1v-2a2 2 0 0 1 0-8Z" />
      <path d="M12 7v2M12 15v2" />
    </svg>
  );
}
