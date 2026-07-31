"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { RequireAuth } from "@/components/RequireAuth";
import { Alert, Button, Card, PageHeading } from "@/components/ui";
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
    <main className="mx-auto w-full max-w-3xl px-6 py-12">
      <div className="mb-6 flex items-center justify-between gap-4">
        <PageHeading title="Support tickets" />
        <Link href="/support/new">
          <Button type="button">Raise an issue</Button>
        </Link>
      </div>

      {query.isPending ? (
        <p className="text-sm text-neutral-500">Loading…</p>
      ) : query.isError ? (
        <Alert>{describeError(query.error)}</Alert>
      ) : query.data.length === 0 ? (
        <Card title="No tickets yet">
          <p className="text-sm text-neutral-600 dark:text-neutral-400">
            You haven&apos;t raised any support tickets. If something&apos;s wrong with a booking or your
            account, let us know.
          </p>
        </Card>
      ) : (
        <ul className="flex flex-col gap-3">
          {query.data.map((ticket) => (
            <li key={ticket.id}>
              <Link
                href={`/support/${ticket.id}`}
                className="block rounded-xl border border-black/10 bg-white p-4 shadow-sm transition-colors hover:bg-black/[0.02] dark:border-white/15 dark:bg-neutral-900 dark:hover:bg-white/5"
              >
                <div className="flex items-center justify-between gap-3">
                  <span className="font-medium">{ticket.subject}</span>
                  <span className="text-xs font-medium uppercase tracking-wide text-neutral-500">
                    {statusLabel(ticket.status)}
                  </span>
                </div>
                <p className="mt-1 text-sm text-neutral-600 dark:text-neutral-400">
                  {categoryLabel(ticket.category)} · {new Date(ticket.createdAtUtc).toLocaleDateString()}
                </p>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </main>
  );
}
