"use client";

import * as signalR from "@microsoft/signalr";
import { useMutation, useQuery } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { Alert, Button, Card, cx } from "@/components/ui";
import { API_BASE_URL, API_V1, apiFetch, describeError } from "@/lib/api";
import { getAccessToken } from "@/lib/auth";
import { ChatContextType, ChatSenderType } from "@/lib/types";
import type { ChatMessagePageResult, ChatMessageResponse, ChatThreadResponse } from "@/lib/types";

/**
 * Chat panel embedded on a booking detail or support ticket detail page
 * (task 192). REST is the real send/read path (task 190/191 - works even if
 * the socket never connects); the SignalR hub at /hubs/chat only pushes live
 * updates to a thread this client has already GETten/POSTed through once.
 */
export function ChatWidget({ contextType, contextId }: { contextType: ChatContextType; contextId: string }) {
  const [draft, setDraft] = useState("");
  const [liveMessages, setLiveMessages] = useState<ChatMessageResponse[]>([]);
  const bottomRef = useRef<HTMLDivElement>(null);

  const threadQuery = useQuery({
    queryKey: ["chat-thread", contextType, contextId],
    queryFn: () =>
      apiFetch<ChatThreadResponse>(`${API_V1}/chat/threads`, {
        method: "POST",
        authenticated: true,
        body: JSON.stringify({ contextType, contextId }),
      }),
  });

  const threadId = threadQuery.data?.id;

  const historyQuery = useQuery({
    queryKey: ["chat-messages", threadId],
    queryFn: () =>
      apiFetch<ChatMessagePageResult>(`${API_V1}/chat/threads/${threadId}/messages?pageSize=100`, {
        authenticated: true,
      }),
    enabled: !!threadId,
  });

  // Server history plus anything that arrived live since it loaded, deduped
  // by id - both our own just-sent message (added directly, in case the
  // socket never connects - REST is the real send path, same as the
  // backend's design) and the hub's echo of that same message can land here.
  const messages = dedupeById([...(historyQuery.data?.messages ?? []), ...liveMessages]);

  const sendMutation = useMutation({
    mutationFn: (body: string) =>
      apiFetch<ChatMessageResponse>(`${API_V1}/chat/threads/${threadId}/messages`, {
        method: "POST",
        authenticated: true,
        body: JSON.stringify({ body }),
      }),
    onSuccess: (message) => {
      setDraft("");
      setLiveMessages((prev) => [...prev, message]);
    },
  });

  const markReadMutation = useMutation({
    mutationFn: () => apiFetch(`${API_V1}/chat/threads/${threadId}/read`, { method: "POST", authenticated: true }),
  });

  useEffect(() => {
    if (!threadId) return;

    // "/hubs/chat" mirrors ChatHubRoutes.ChatPath; accessTokenFactory (rather
    // than an Authorization header, which a browser can't set on a WebSocket
    // handshake) puts the JWT on ?access_token= for the client automatically -
    // exactly what ChatHubJwtEvents.OnMessageReceived reads back server-side.
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/hubs/chat`, { accessTokenFactory: () => getAccessToken() ?? "" })
      .withAutomaticReconnect()
      .build();

    // "MessageReceived" mirrors ChatHubBroadcastHandler.MessageReceivedMethod.
    connection.on("MessageReceived", (message: ChatMessageResponse) => {
      if (message.threadId !== threadId) return;
      setLiveMessages((prev) => [...prev, message]);
      if (message.senderType !== ChatSenderType.Customer) {
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
        // Live delivery degrades to REST-only (a manual refetch); the send/
        // read paths above never depend on the socket being up.
      });

    return () => {
      connection.invoke("LeaveThread", threadId).catch(() => {});
      connection.stop();
    };
    // Reconnects only when the thread changes - markReadMutation is a fresh
    // object every render (useMutation) and would otherwise tear the socket
    // down and reconnect on every render.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [threadId]);

  useEffect(() => {
    if (threadId && messages.some((m) => m.senderType !== ChatSenderType.Customer && !m.readAtUtc)) {
      markReadMutation.mutate();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [threadId, messages.length]);

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages.length]);

  function handleSend() {
    const body = draft.trim();
    if (!body || !threadId) return;
    sendMutation.mutate(body);
  }

  if (threadQuery.isPending || historyQuery.isPending) {
    return (
      <Card title="Chat">
        <p className="text-sm text-fg-muted">Loading chat…</p>
      </Card>
    );
  }

  if (threadQuery.isError) {
    return (
      <Card title="Chat">
        <Alert>{describeError(threadQuery.error)}</Alert>
      </Card>
    );
  }

  return (
    <Card title="Chat">
      <div className="flex max-h-96 flex-col gap-3 overflow-y-auto rounded-lg border border-line p-3">
        {messages.length === 0 ? (
          <p className="text-sm text-fg-muted">No messages yet — say hello.</p>
        ) : (
          messages.map((message) => {
            const fromCustomer = message.senderType === ChatSenderType.Customer;
            return (
              <div
                key={message.id}
                className={cx(
                  "max-w-[80%] rounded-2xl px-3 py-2 text-sm",
                  fromCustomer
                    ? "self-end rounded-br-md bg-brand-600 text-fg-on-brand"
                    : "self-start rounded-bl-md bg-surface-2 text-fg",
                )}
              >
                {/* Side and colour are the only visual cue for who spoke, and
                    neither reaches a screen reader. */}
                <span className="sr-only">{fromCustomer ? "You said:" : "Support said:"}</span>
                <p>{message.body}</p>
                <p
                  className={cx(
                    "nums mt-1 text-xs",
                    fromCustomer ? "text-fg-on-brand/70" : "text-fg-subtle",
                  )}
                >
                  {new Date(message.sentAtUtc).toLocaleTimeString()}
                </p>
              </div>
            );
          })
        )}
        <div ref={bottomRef} />
      </div>

      {sendMutation.isError ? (
        <div className="mt-2">
          <Alert>{describeError(sendMutation.error)}</Alert>
        </div>
      ) : null}

      <form
        className="mt-3 flex gap-2"
        onSubmit={(event) => {
          event.preventDefault();
          handleSend();
        }}
      >
        <label htmlFor="chat-message" className="sr-only">
          Message
        </label>
        <input
          id="chat-message"
          type="text"
          maxLength={4000}
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
          placeholder="Type a message…"
          className="min-w-0 flex-1 rounded-lg border border-line bg-surface px-3 py-2 text-sm text-fg shadow-xs outline-none transition duration-fast ease-out placeholder:text-fg-subtle hover:border-line-strong focus:border-brand-600 focus:ring-2 focus:ring-brand-600/25"
        />
        <Button type="submit" loading={sendMutation.isPending} disabled={!draft.trim()}>
          Send
        </Button>
      </form>
    </Card>
  );
}

/** Later entries win (e.g. a read-receipt update arriving after the original send). */
function dedupeById(messages: ChatMessageResponse[]): ChatMessageResponse[] {
  const byId = new Map(messages.map((m) => [m.id, m]));
  return Array.from(byId.values()).sort((a, b) => a.sentAtUtc.localeCompare(b.sentAtUtc));
}
