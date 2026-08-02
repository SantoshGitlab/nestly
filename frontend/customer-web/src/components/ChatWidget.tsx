"use client";

import * as signalR from "@microsoft/signalr";
import { useMutation, useQuery } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { Alert, Button, Card } from "@/components/ui";
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
  const connectionRef = useRef<signalR.HubConnection | null>(null);
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

  // Server history plus anything that arrived live since it loaded - avoids
  // waiting for a refetch to show a message this client just received.
  const messages = [...(historyQuery.data?.messages ?? []), ...liveMessages];

  const sendMutation = useMutation({
    mutationFn: (body: string) =>
      apiFetch<ChatMessageResponse>(`${API_V1}/chat/threads/${threadId}/messages`, {
        method: "POST",
        authenticated: true,
        body: JSON.stringify({ body }),
      }),
    onSuccess: () => setDraft(""),
  });

  const markReadMutation = useMutation({
    mutationFn: () => apiFetch(`${API_V1}/chat/threads/${threadId}/read`, { method: "POST", authenticated: true }),
  });

  useEffect(() => {
    if (!threadId) return;

    const token = getAccessToken();
    const connection = new signalR.HubConnectionBuilder()
      .withUrl(`${API_BASE_URL}/hubs/chat`, { accessTokenFactory: () => token ?? "" })
      .withAutomaticReconnect()
      .build();

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

    connectionRef.current = connection;

    return () => {
      connection.invoke("LeaveThread", threadId).catch(() => {});
      connection.stop();
      connectionRef.current = null;
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
        <p className="text-sm text-neutral-500">Loading chat…</p>
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
      <div className="flex max-h-96 flex-col gap-3 overflow-y-auto rounded-lg border border-black/10 p-3 dark:border-white/10">
        {messages.length === 0 ? (
          <p className="text-sm text-neutral-500">No messages yet - say hello.</p>
        ) : (
          messages.map((message) => (
            <div
              key={message.id}
              className={`max-w-[80%] rounded-lg px-3 py-2 text-sm ${
                message.senderType === ChatSenderType.Customer
                  ? "self-end bg-black text-white dark:bg-white dark:text-black"
                  : "self-start bg-neutral-100 dark:bg-neutral-800"
              }`}
            >
              <p>{message.body}</p>
              <p className="mt-1 text-xs opacity-60">{new Date(message.sentAtUtc).toLocaleTimeString()}</p>
            </div>
          ))
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
        <input
          type="text"
          value={draft}
          onChange={(e) => setDraft(e.target.value)}
          placeholder="Type a message…"
          className="flex-1 rounded-lg border border-black/15 bg-transparent px-3 py-2 text-sm outline-none focus:border-black focus:ring-1 focus:ring-black dark:border-white/20 dark:focus:border-white dark:focus:ring-white"
        />
        <Button type="submit" disabled={sendMutation.isPending || !draft.trim()}>
          Send
        </Button>
      </form>
    </Card>
  );
}
