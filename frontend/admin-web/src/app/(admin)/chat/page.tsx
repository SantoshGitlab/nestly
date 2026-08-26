"use client";

import { keepPreviousData, useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { Badge, PageHeading } from "@/components/ui";
import { DataTable, Pagination, formatDateTime } from "@/components/data-table";
import type { DataTableColumn } from "@/components/data-table";
import { listChatThreads } from "@/lib/chat-api";
import { ChatContextType } from "@/lib/chat-types";
import type { AdminChatThreadSummaryResponse } from "@/lib/chat-types";

const PAGE_SIZE = 20;

/**
 * The thread detail page needs the customer's name and the booking/ticket
 * link for its header, but no endpoint returns "one thread with its
 * counterpart's display info" - only the inbox listing does. Carrying it
 * through the URL (same technique as `addresses/new?returnTo=` and
 * `login?next=` elsewhere in this app) avoids a second backend endpoint for
 * data this page already has in hand.
 */
function threadHref(row: AdminChatThreadSummaryResponse): string {
  const params = new URLSearchParams({
    customerName: row.customerName,
    contextType: String(row.contextType),
    contextId: row.contextId,
  });
  return `/chat/${row.threadId}?${params.toString()}`;
}

/**
 * Support-console chat inbox (PRODUCT-ENHANCEMENTS.md IN-APP CHAT, task 193
 * follow-up): every thread across every customer, most recent first - the
 * page an admin opens *before* knowing which specific booking/ticket thread
 * they want, which is the entry point AdminChatController's existing
 * reply/history/read endpoints never had.
 *
 * Polls rather than subscribing to a live broadcast: unlike a single open
 * thread (which joins that thread's SignalR group - see [threadId]/page.tsx),
 * there is no "inbox" group a connection could join for a push the moment
 * any customer, anywhere, sends a message. A 15s poll is a deliberately
 * small addition on top of the real-time single-thread view, not a
 * substitute for it.
 */
export default function ChatInboxPage() {
  const router = useRouter();
  const [page, setPage] = useState(1);

  const query = useQuery({
    queryKey: ["admin-chat-threads", page],
    queryFn: () => listChatThreads(page, PAGE_SIZE),
    placeholderData: keepPreviousData,
    refetchInterval: 15_000,
  });

  const columns: DataTableColumn<AdminChatThreadSummaryResponse>[] = [
    {
      key: "customer",
      header: "Customer",
      cell: (row) => (
        <span>
          <span className="font-medium text-fg">{row.customerName}</span>
          {row.customerMobile ? <span className="nums block text-xs text-fg-subtle">{row.customerMobile}</span> : null}
        </span>
      ),
    },
    {
      key: "context",
      header: "Regarding",
      cell: (row) => (
        <Link
          href={row.contextType === ChatContextType.Booking ? `/bookings/${row.contextId}` : `/support/${row.contextId}`}
          onClick={(e) => e.stopPropagation()}
          className="text-sm text-fg-muted underline-offset-4 hover:text-brand-600 hover:underline dark:hover:text-brand-400"
        >
          {row.contextType === ChatContextType.Booking ? "Booking" : "Support ticket"}
        </Link>
      ),
    },
    {
      key: "lastMessage",
      header: "Last message",
      cell: (row) => <span className="nums text-sm text-fg-muted">{formatDateTime(row.lastMessageAtUtc)}</span>,
    },
    {
      key: "unread",
      header: "Unread",
      cell: (row) =>
        row.unreadCount > 0 ? (
          <Badge tone="accent">{row.unreadCount}</Badge>
        ) : (
          <span className="text-sm text-fg-subtle">—</span>
        ),
    },
  ];

  return (
    <div className="w-full max-w-5xl">
      <PageHeading
        title="Chat"
        subtitle="Every customer conversation across bookings and support tickets, most recent first (PRODUCT-ENHANCEMENTS.md IN-APP CHAT)."
      />

      <div className="mt-6">
        <DataTable
          title="Conversations"
          columns={columns}
          rows={query.data?.items}
          rowKey={(row) => row.threadId}
          onRowClick={(row) => router.push(threadHref(row))}
          isLoading={query.isPending}
          isFetching={query.isFetching}
          error={query.error}
          onRetry={() => query.refetch()}
          skeletonRows={8}
          minWidth="720px"
          caption="Chat threads across every customer"
          emptyTitle="No conversations yet"
          emptyDescription="Threads appear here as soon as a customer messages about a booking or support ticket."
          footer={
            query.data ? (
              <Pagination
                page={page}
                pageSize={PAGE_SIZE}
                totalCount={query.data.totalCount}
                onPageChange={setPage}
                busy={query.isFetching}
                itemLabel="conversation"
              />
            ) : null
          }
        />
      </div>
    </div>
  );
}
