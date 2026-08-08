"use client";

import * as signalR from "@microsoft/signalr";
import { useMutation, useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useParams, useSearchParams } from "next/navigation";
import { Suspense, useEffect, useRef, useState } from "react";
import { API_BASE_URL, describeError } from "@/lib/api";
import { getAccessToken } from "@/lib/auth";
import { getChatHistory, markChatThreadRead, replyToChatThread } from "@/lib/chat-api";
import { ChatContextType, ChatSenderType } from "@/lib/chat-types";
import type { ChatMessageResponse } from "@/lib/chat-types";
import { Alert, Button, Card, PageHeading, Skeleton, cx } from "@/components/ui";

/**
 * Support-console reply view for a single thread (task 193): message
 * history plus a composer that sends as {@link ChatSenderType.Admin}, live
 * updates via the same `/hubs/chat` SignalR hub the customer side uses -
 * `ChatHub.CanAccessAsync` already authorizes any admin holding "chat.read"
 * into any thread, so no server-side change was needed to reuse it here.
 *
 * REST is still the real send path (mirrors customer-web's ChatWidget,
 * which documents why: it works even if the socket never connects). The
 * hub is purely for live delivery of the *other* side's messages while this
 * page is open.
 */
export default function AdminChatThreadPage() {
  return (
    <Suspense fallback={<ChatThreadSkeleton />}>
      <AdminChatThreadScreen />
    </Suspense>
  );
}

function AdminChatThreadScreen() {
  const params = useParams<{ threadId: string }>();
  const threadId = params.threadId;
  const searchParams = useSearchParams();
  const customerName = searchParams.get("customerName");
  const contextType = searchParams.get("contextType");
  const contextId = searchParams.get("contextId");

  const [draft, setDraft] = useState("");
  const [liveMessages, setLiveMessages] = useState<ChatMessageResponse[]>([]);
  const bottomRef = useRef<HTMLDivElement>(null);

  const historyQuery = useQuery({
    queryKey: ["admin-chat-messages", threadId],
    queryFn: () => getChatHistory(threadId),
  });

  const messages = dedupeById([...(historyQuery.data?.messages ?? []), ...liveMessages]);

  const sendMutation = useMutation({
    mutationFn: (body: string) => replyToChatThread(threadId, body),
    onSuccess: (message) => {
      setDraft("");
      setLiveMessages((prev) => [...prev, message]);
    },
  });

  const markReadMutation = useMutation({
    mutationFn: () => markChatThreadRead(threadId),
  });

  useEffect(() => {
    const connection = new signalR.HubConnectionBuilder()
      // withCredentials: false - auth rides the query-string token via
      // accessTokenFactory, not cookies (admin-api's CORS policy is
      // credential-less by design, see AddNestlyCors); matches
      // customer-web's ChatWidget/useBookingTracking connections to the
      // same hub. Without this the browser's default XHR withCredentials:true
      // makes SignalR's negotiate preflight require
      // Access-Control-Allow-Credentials: true, which the API never sends,
      // so the connection is blocked by CORS before it starts.
      .withUrl(`${API_BASE_URL}/hubs/chat`, { accessTokenFactory: () => getAccessToken() ?? "", withCredentials: false })
      .withAutomaticReconnect()
      .build();

    connection.on("MessageReceived", (message: ChatMessageResponse) => {
      if (message.threadId !== threadId) return;
      setLiveMessages((prev) => [...prev, message]);
      if (message.senderType !== ChatSenderType.Admin) {
        markReadMutation.mutate();
      }
    });

    connection.onreconnected(() => {
      connection.invoke("JoinThread", threadId).catch(() => {});
    });

    connection
      .start()
      .then(() => connection.invoke("JoinThread", threadId))
      .catch(() => {
        // Live delivery degrades to whatever is already loaded; the send/
        // read paths above never depend on the socket being up.
      });

    return () => {
      connection.invoke("LeaveThread", threadId).catch(() => {});
      connection.stop();
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [threadId]);

  useEffect(() => {
    if (messages.some((m) => m.senderType !== ChatSenderType.Admin && !m.readAtUtc)) {
      markReadMutation.mutate();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [threadId, messages.length]);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages.length]);

  function handleSend() {
    const body = draft.trim();
    if (!body) return;
    sendMutation.mutate(body);
  }

  const contextHref =
    contextType !== null && contextId
      ? Number(contextType) === ChatContextType.Booking
        ? `/bookings/${contextId}`
        : `/support/${contextId}`
      : null;
  const contextLabel = contextType !== null ? (Number(contextType) === ChatContextType.Booking ? "booking" : "support ticket") : null;

  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-4">
      <PageHeading
        title={customerName ?? "Chat"}
        actions={
          <Link
            href="/chat"
            className="inline-flex h-9 items-center justify-center rounded-lg border border-line bg-surface px-3 text-sm font-medium text-fg shadow-xs transition duration-fast ease-out hover:border-line-strong hover:bg-surface-2"
          >
            Back to inbox
          </Link>
        }
      />

      {contextHref && contextLabel ? (
        <p className="-mt-3 text-sm text-fg-muted">
          Regarding this{" "}
          <Link href={contextHref} className="underline-offset-4 hover:underline">
            {contextLabel}
          </Link>
          .
        </p>
      ) : null}

      <Card title="Conversation">
        {historyQuery.isPending ? (
          <ChatSkeleton />
        ) : historyQuery.isError ? (
          <Alert
            tone="error"
            title="Couldn't load these messages"
            action={
              <Button size="sm" variant="secondary" loading={historyQuery.isRefetching} onClick={() => historyQuery.refetch()}>
                Retry
              </Button>
            }
          >
            {describeError(historyQuery.error)}
          </Alert>
        ) : (
          <>
            <div className="flex max-h-[28rem] flex-col gap-3 overflow-y-auto rounded-lg border border-line p-3">
              {messages.length === 0 ? (
                <p className="text-sm text-fg-muted">No messages yet.</p>
              ) : (
                messages.map((message) => {
                  const fromAdmin = message.senderType === ChatSenderType.Admin;
                  return (
                    <div
                      key={message.id}
                      className={cx(
                        "max-w-[80%] rounded-2xl px-3 py-2 text-sm",
                        fromAdmin
                          ? "self-end rounded-br-md bg-brand-600 text-fg-on-brand"
                          : "self-start rounded-bl-md bg-surface-2 text-fg",
                      )}
                    >
                      <span className="sr-only">{fromAdmin ? "You said:" : "Customer said:"}</span>
                      <p>{message.body}</p>
                      <p className={cx("nums mt-1 text-xs", fromAdmin ? "text-fg-on-brand/70" : "text-fg-subtle")}>
                        {new Date(message.sentAtUtc).toLocaleString()}
                      </p>
                    </div>
                  );
                })
              )}
              <div ref={bottomRef} />
            </div>

            {sendMutation.isError ? (
              <div className="mt-2">
                <Alert
                  tone="error"
                  title="Your reply didn't send"
                  action={
                    sendMutation.variables ? (
                      <Button
                        size="sm"
                        variant="secondary"
                        loading={sendMutation.isPending}
                        onClick={() => sendMutation.mutate(sendMutation.variables as string)}
                      >
                        Try again
                      </Button>
                    ) : undefined
                  }
                >
                  {describeError(sendMutation.error)}
                </Alert>
              </div>
            ) : null}

            <form
              className="mt-3 flex gap-2"
              onSubmit={(event) => {
                event.preventDefault();
                handleSend();
              }}
            >
              <label htmlFor="admin-chat-message" className="sr-only">
                Reply
              </label>
              <input
                id="admin-chat-message"
                type="text"
                maxLength={4000}
                value={draft}
                onChange={(e) => setDraft(e.target.value)}
                placeholder="Type a reply…"
                className="min-w-0 flex-1 rounded-lg border border-line bg-surface px-3 py-2 text-sm text-fg shadow-xs outline-none transition duration-fast ease-out placeholder:text-fg-subtle hover:border-line-strong focus:border-brand-600 focus:ring-2 focus:ring-brand-600/25"
              />
              <Button type="submit" loading={sendMutation.isPending} disabled={!draft.trim()}>
                Send
              </Button>
            </form>
          </>
        )}
      </Card>
    </div>
  );
}

/** Later entries win (e.g. a read-receipt update arriving after the original send) - mirrors customer-web's ChatWidget. */
function dedupeById(messages: ChatMessageResponse[]): ChatMessageResponse[] {
  const byId = new Map(messages.map((m) => [m.id, m]));
  return Array.from(byId.values()).sort((a, b) => a.sentAtUtc.localeCompare(b.sentAtUtc));
}

function ChatSkeleton() {
  const bubbles = [
    { fromAdmin: false, width: "w-3/5" },
    { fromAdmin: true, width: "w-2/5" },
    { fromAdmin: false, width: "w-1/2" },
  ];

  return (
    <div aria-hidden>
      <div className="flex flex-col gap-3 rounded-lg border border-line p-3">
        {bubbles.map((bubble, index) => (
          <Skeleton
            key={index}
            className={cx("h-14 rounded-2xl", bubble.width, bubble.fromAdmin ? "self-end rounded-br-md" : "self-start rounded-bl-md")}
          />
        ))}
      </div>
      <div className="mt-3 flex gap-2">
        <Skeleton className="h-[38px] min-w-0 flex-1" />
        <Skeleton className="h-[38px] w-20 rounded-lg" />
      </div>
    </div>
  );
}

function ChatThreadSkeleton() {
  return (
    <div className="mx-auto flex w-full max-w-3xl flex-col gap-4">
      <div>
        <Skeleton className="h-3 w-24" />
        <Skeleton className="mt-3 h-8 w-56" />
      </div>
      <div className="rounded-2xl border border-line bg-surface p-6 shadow-sm">
        <Skeleton className="h-4 w-32" />
        <div className="mt-5">
          <ChatSkeleton />
        </div>
      </div>
    </div>
  );
}
