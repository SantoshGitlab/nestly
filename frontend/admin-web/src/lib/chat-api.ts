/**
 * Typed client for the Admin API's support-console chat surface
 * (PRODUCT-ENHANCEMENTS.md IN-APP CHAT, task 193): `ChatController` - the
 * inbox listing every thread across every customer, plus history/reply/
 * read-receipt on a single thread once opened.
 */
import { API_V1, apiFetch } from "./api";
import type {
  AdminChatThreadListResponse,
  ChatContextType,
  ChatMessagePageResult,
  ChatMessageResponse,
} from "./chat-types";

const CHAT_BASE = `${API_V1}/chat/threads`;

export const listChatThreads = (page = 1, pageSize = 20) =>
  apiFetch<AdminChatThreadListResponse>(`${CHAT_BASE}?page=${page}&pageSize=${pageSize}`, { authenticated: true });

export const getOrCreateChatThread = (contextType: ChatContextType, contextId: string) =>
  apiFetch<{ id: string; contextType: ChatContextType; contextId: string; createdAtUtc: string; lastMessageAtUtc: string }>(
    CHAT_BASE,
    { method: "POST", authenticated: true, body: JSON.stringify({ contextType, contextId }) },
  );

export const getChatHistory = (threadId: string, page = 1, pageSize = 100) =>
  apiFetch<ChatMessagePageResult>(`${CHAT_BASE}/${threadId}/messages?page=${page}&pageSize=${pageSize}`, {
    authenticated: true,
  });

export const replyToChatThread = (threadId: string, body: string) =>
  apiFetch<ChatMessageResponse>(`${CHAT_BASE}/${threadId}/messages`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify({ body }),
  });

export const markChatThreadRead = (threadId: string) =>
  apiFetch(`${CHAT_BASE}/${threadId}/read`, { method: "POST", authenticated: true });
