"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useState } from "react";
import type { FormEvent } from "react";
import { Alert, Button, Card, Field, PageHeading } from "@/components/ui";
import { describeError } from "@/lib/api";
import { searchSupportTickets } from "@/lib/support-api";
import {
  CATEGORY_FILTER_OPTIONS,
  DEFAULT_SUPPORT_TICKET_FILTERS,
  PRIORITY_FILTER_OPTIONS,
  STATUS_FILTER_OPTIONS,
  UNASSIGNED_FILTER_OPTIONS,
  buildSupportTicketSearchQuery,
  categoryLabel,
  priorityLabel,
  statusLabel,
} from "@/lib/support";
import type { SupportTicketFilters } from "@/lib/support";

const PAGE_SIZE = 20;
const selectClassName =
  "rounded-lg border border-black/15 bg-transparent px-3 py-2 text-sm outline-none focus:border-black focus:ring-1 focus:ring-black dark:border-white/20 dark:focus:border-white dark:focus:ring-white";
const labelClassName = "flex flex-col gap-1.5 text-sm font-medium";

/**
 * Admin ticket list screen (SRS 12.14.1, tasks 120f/121): filterable search
 * across every customer's tickets (status, category, priority, customer,
 * booking, assignee, unassigned-only, date range), linking each row through
 * to the workflow detail screen at `/support/[id]` where assign/respond/
 * escalate/resolve/link-booking actually happen. Mirrors
 * `(admin)/reviews/page.tsx` and `(admin)/customers/page.tsx`'s
 * filter-form-plus-table shape.
 */
export default function SupportTicketsPage() {
  const [draft, setDraft] = useState<SupportTicketFilters>(DEFAULT_SUPPORT_TICKET_FILTERS);
  const [applied, setApplied] = useState<SupportTicketFilters>(DEFAULT_SUPPORT_TICKET_FILTERS);
  const [page, setPage] = useState(1);

  const query = useQuery({
    queryKey: ["admin-support-tickets", applied, page],
    queryFn: () => searchSupportTickets(buildSupportTicketSearchQuery(applied, { page, pageSize: PAGE_SIZE })),
  });

  const onApply = (event: FormEvent) => {
    event.preventDefault();
    setPage(1);
    setApplied(draft);
  };

  const onReset = () => {
    setDraft(DEFAULT_SUPPORT_TICKET_FILTERS);
    setApplied(DEFAULT_SUPPORT_TICKET_FILTERS);
    setPage(1);
  };

  const totalPages = query.data ? Math.max(1, Math.ceil(query.data.totalCount / PAGE_SIZE)) : 1;

  return (
    <div className="mx-auto w-full max-w-5xl">
      <PageHeading
        title="Support Tickets"
        subtitle="Search, assign, respond to and resolve customer support tickets across every customer (SRS 12.14)."
      />

      <Card title="Filters" description="All fields are optional; leave a field blank to not filter on it.">
        <form onSubmit={onApply} className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <label className={labelClassName}>
            Status
            <select
              className={selectClassName}
              value={draft.status}
              onChange={(e) => setDraft((f) => ({ ...f, status: e.target.value as SupportTicketFilters["status"] }))}
            >
              {STATUS_FILTER_OPTIONS.map((option) => (
                <option key={option.label} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>

          <label className={labelClassName}>
            Category
            <select
              className={selectClassName}
              value={draft.category}
              onChange={(e) => setDraft((f) => ({ ...f, category: e.target.value as SupportTicketFilters["category"] }))}
            >
              {CATEGORY_FILTER_OPTIONS.map((option) => (
                <option key={option.label} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>

          <label className={labelClassName}>
            Priority
            <select
              className={selectClassName}
              value={draft.priority}
              onChange={(e) => setDraft((f) => ({ ...f, priority: e.target.value as SupportTicketFilters["priority"] }))}
            >
              {PRIORITY_FILTER_OPTIONS.map((option) => (
                <option key={option.label} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>

          <label className={labelClassName}>
            Assignment
            <select
              className={selectClassName}
              value={draft.unassigned}
              onChange={(e) => setDraft((f) => ({ ...f, unassigned: e.target.value as SupportTicketFilters["unassigned"] }))}
            >
              {UNASSIGNED_FILTER_OPTIONS.map((option) => (
                <option key={option.label} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </label>

          <Field
            label="Customer ID"
            placeholder="Customer GUID"
            value={draft.customerId}
            onChange={(e) => setDraft((f) => ({ ...f, customerId: e.target.value }))}
          />

          <Field
            label="Booking ID"
            placeholder="Booking GUID"
            value={draft.bookingId}
            onChange={(e) => setDraft((f) => ({ ...f, bookingId: e.target.value }))}
          />

          <Field
            label="Assigned admin ID"
            placeholder="Admin user GUID"
            value={draft.assignedAdminUserId}
            onChange={(e) => setDraft((f) => ({ ...f, assignedAdminUserId: e.target.value }))}
          />

          <div className="grid grid-cols-2 gap-2">
            <Field
              label="Created from"
              type="date"
              value={draft.fromDate}
              onChange={(e) => setDraft((f) => ({ ...f, fromDate: e.target.value }))}
            />
            <Field
              label="Created to"
              type="date"
              value={draft.toDate}
              onChange={(e) => setDraft((f) => ({ ...f, toDate: e.target.value }))}
            />
          </div>

          <div className="flex items-end gap-2">
            <Button type="submit">Apply filters</Button>
            <Button type="button" variant="secondary" onClick={onReset}>
              Reset
            </Button>
          </div>
        </form>
      </Card>

      <div className="mt-6">
        {query.isPending ? (
          <p className="text-sm text-neutral-500">Loading tickets…</p>
        ) : query.isError ? (
          <Alert>{describeError(query.error)}</Alert>
        ) : query.data.items.length === 0 ? (
          <Card title="No tickets match these filters">
            <p className="text-sm text-neutral-600 dark:text-neutral-400">Try broadening your search criteria.</p>
          </Card>
        ) : (
          <>
            <div className="overflow-x-auto rounded-xl border border-black/10 dark:border-white/15">
              <table className="w-full min-w-[900px] text-left text-sm">
                <thead className="border-b border-black/10 bg-black/5 dark:border-white/15 dark:bg-white/5">
                  <tr>
                    <th className="px-4 py-3 font-medium">Subject</th>
                    <th className="px-4 py-3 font-medium">Customer</th>
                    <th className="px-4 py-3 font-medium">Category</th>
                    <th className="px-4 py-3 font-medium">Priority</th>
                    <th className="px-4 py-3 font-medium">Status</th>
                    <th className="px-4 py-3 font-medium">Assigned to</th>
                    <th className="px-4 py-3 font-medium">Created</th>
                  </tr>
                </thead>
                <tbody>
                  {query.data.items.map((ticket) => (
                    <tr
                      key={ticket.id}
                      className="border-b border-black/5 last:border-0 hover:bg-black/[.03] dark:border-white/10 dark:hover:bg-white/[.05]"
                    >
                      <td className="px-4 py-3">
                        <Link href={`/support/${ticket.id}`} className="font-medium underline-offset-2 hover:underline">
                          {ticket.subject}
                        </Link>
                        {ticket.isDisputed ? (
                          <span className="ml-2 inline-flex rounded-full border border-amber-300 bg-amber-50 px-2 py-0.5 text-xs font-medium text-amber-900 dark:border-amber-800 dark:bg-amber-950 dark:text-amber-100">
                            Disputed
                          </span>
                        ) : null}
                      </td>
                      <td className="px-4 py-3">{ticket.customerName}</td>
                      <td className="px-4 py-3">{categoryLabel(ticket.category)}</td>
                      <td className="px-4 py-3">{priorityLabel(ticket.priority)}</td>
                      <td className="px-4 py-3">{statusLabel(ticket.status)}</td>
                      <td className="px-4 py-3">{ticket.assignedAdminName ?? "Unassigned"}</td>
                      <td className="px-4 py-3">{new Date(ticket.createdAtUtc).toLocaleDateString()}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <div className="mt-4 flex items-center justify-between text-sm">
              <span className="text-neutral-600 dark:text-neutral-400">
                Page {page} of {totalPages} · {query.data.totalCount} ticket{query.data.totalCount === 1 ? "" : "s"}
              </span>
              <div className="flex gap-2">
                <Button variant="secondary" disabled={page <= 1} onClick={() => setPage((p) => Math.max(1, p - 1))}>
                  Previous
                </Button>
                <Button
                  variant="secondary"
                  disabled={page >= totalPages}
                  onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                >
                  Next
                </Button>
              </div>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
