"use client";

import { keepPreviousData, useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useState } from "react";
import { Badge, Button, Field, PageHeading, Select } from "@/components/ui";
import { DataTable, FilterBar, Pagination, countActiveFilters, formatDate } from "@/components/data-table";
import type { DataTableColumn } from "@/components/data-table";
import { TicketPriorityBadge, TicketStatusBadge } from "@/components/status-badges";
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
import type { AdminSupportTicketSummaryResponse } from "@/lib/support-types";

const PAGE_SIZE = 20;

/**
 * Admin ticket list screen (SRS 12.14.1, tasks 120f/121): filterable search
 * across every customer's tickets (status, category, priority, customer,
 * booking, assignee, unassigned-only, date range), linking each row through
 * to the workflow detail screen at `/support/[id]` where assign/respond/
 * escalate/resolve/link-booking actually happen.
 *
 * Columns are deliberately not sortable: the list is paged server-side and
 * the endpoint takes no sort parameter, so a header sort would reorder only
 * the 20 rows on screen.
 */
export default function SupportTicketsPage() {
  const [draft, setDraft] = useState<SupportTicketFilters>(DEFAULT_SUPPORT_TICKET_FILTERS);
  const [applied, setApplied] = useState<SupportTicketFilters>(DEFAULT_SUPPORT_TICKET_FILTERS);
  const [page, setPage] = useState(1);

  const query = useQuery({
    queryKey: ["admin-support-tickets", applied, page],
    queryFn: () => searchSupportTickets(buildSupportTicketSearchQuery(applied, { page, pageSize: PAGE_SIZE })),
    placeholderData: keepPreviousData,
  });

  const onApply = () => {
    setPage(1);
    setApplied(draft);
  };

  const onReset = () => {
    setDraft(DEFAULT_SUPPORT_TICKET_FILTERS);
    setApplied(DEFAULT_SUPPORT_TICKET_FILTERS);
    setPage(1);
  };

  const columns: DataTableColumn<AdminSupportTicketSummaryResponse>[] = [
    {
      key: "subject",
      header: "Subject",
      className: "max-w-sm",
      cell: (ticket) => (
        <div className="flex flex-wrap items-center gap-2">
          <Link
            href={`/support/${ticket.id}`}
            className="font-medium text-fg underline-offset-4 hover:text-brand-600 hover:underline dark:hover:text-brand-400"
          >
            {ticket.subject}
          </Link>
          {ticket.isDisputed ? <Badge tone="warning">Disputed</Badge> : null}
        </div>
      ),
    },
    { key: "customer", header: "Customer", cell: (ticket) => ticket.customerName },
    { key: "category", header: "Category", cell: (ticket) => categoryLabel(ticket.category) },
    {
      key: "priority",
      header: "Priority",
      cell: (ticket) => (
        <TicketPriorityBadge priority={ticket.priority} label={priorityLabel(ticket.priority)} />
      ),
    },
    {
      key: "status",
      header: "Status",
      cell: (ticket) => <TicketStatusBadge status={ticket.status} label={statusLabel(ticket.status)} />,
    },
    {
      key: "assignee",
      header: "Assigned to",
      cell: (ticket) =>
        ticket.assignedAdminName ?? <span className="text-fg-subtle">Unassigned</span>,
    },
    {
      key: "created",
      header: "Created",
      cell: (ticket) => <span className="nums">{formatDate(ticket.createdAtUtc)}</span>,
    },
  ];

  return (
    <div className="mx-auto w-full max-w-7xl">
      <PageHeading
        title="Support Tickets"
        subtitle="Search, assign, respond to and resolve customer support tickets across every customer (SRS 12.14)."
      />

      <FilterBar
        onSubmit={onApply}
        onClear={onReset}
        activeCount={countActiveFilters(applied)}
        busy={query.isFetching}
      >
        <Select
          label="Status"
          options={STATUS_FILTER_OPTIONS}
          value={draft.status}
          onChange={(e) => setDraft((f) => ({ ...f, status: e.target.value as SupportTicketFilters["status"] }))}
        />
        <Select
          label="Category"
          options={CATEGORY_FILTER_OPTIONS}
          value={draft.category}
          onChange={(e) => setDraft((f) => ({ ...f, category: e.target.value as SupportTicketFilters["category"] }))}
        />
        <Select
          label="Priority"
          options={PRIORITY_FILTER_OPTIONS}
          value={draft.priority}
          onChange={(e) => setDraft((f) => ({ ...f, priority: e.target.value as SupportTicketFilters["priority"] }))}
        />
        <Select
          label="Assignment"
          options={UNASSIGNED_FILTER_OPTIONS}
          value={draft.unassigned}
          onChange={(e) =>
            setDraft((f) => ({ ...f, unassigned: e.target.value as SupportTicketFilters["unassigned"] }))
          }
        />
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
        <Field
          label="Created from"
          type="date"
          max={draft.toDate || undefined}
          value={draft.fromDate}
          onChange={(e) => setDraft((f) => ({ ...f, fromDate: e.target.value }))}
        />
        <Field
          label="Created to"
          type="date"
          min={draft.fromDate || undefined}
          value={draft.toDate}
          onChange={(e) => setDraft((f) => ({ ...f, toDate: e.target.value }))}
        />
      </FilterBar>

      <div className="mt-6">
        <DataTable
          title="Tickets"
          columns={columns}
          rows={query.data?.items}
          rowKey={(ticket) => ticket.id}
          isLoading={query.isPending}
          isFetching={query.isFetching}
          error={query.error}
          onRetry={() => query.refetch()}
          caption="Support tickets matching the current filters"
          emptyTitle="No tickets match these filters"
          emptyDescription="Try broadening the date range, or clear the filters to see every ticket."
          emptyAction={
            countActiveFilters(applied) > 0 ? (
              <Button variant="secondary" onClick={onReset}>
                Clear filters
              </Button>
            ) : undefined
          }
          skeletonRows={8}
          minWidth="1080px"
          footer={
            query.data ? (
              <Pagination
                page={page}
                pageSize={PAGE_SIZE}
                totalCount={query.data.totalCount}
                onPageChange={setPage}
                busy={query.isFetching}
                itemLabel="ticket"
              />
            ) : null
          }
        />
      </div>
    </div>
  );
}
