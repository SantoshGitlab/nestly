/**
 * Typed client for the Provider API's chat surface (`/api/v1/chat/threads`,
 * task 193's provider reply view). Mirrors customer-web's ChatWidget calls
 * one-for-one - see that component's doc comment for why REST is the real
 * send/read path and the SignalR hub only pushes live updates.
 */
import { API_V1, apiFetch } from "./api";
import type {
  ChatMessagePageResult,
  ChatMessageResponse,
  ChatThreadResponse,
  GetOrCreateChatThreadRequestBody,
  SendChatMessageRequestBody,
} from "./chat-types";

const CHAT_BASE = `${API_V1}/chat/threads`;

export const getOrCreateChatThread = (request: GetOrCreateChatThreadRequestBody) =>
  apiFetch<ChatThreadResponse>(CHAT_BASE, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const getChatHistory = (threadId: string, pageSize = 100) =>
  apiFetch<ChatMessagePageResult>(`${CHAT_BASE}/${threadId}/messages?pageSize=${pageSize}`, {
    authenticated: true,
  });

export const sendChatMessage = (threadId: string, request: SendChatMessageRequestBody) =>
  apiFetch<ChatMessageResponse>(`${CHAT_BASE}/${threadId}/messages`, {
    method: "POST",
    authenticated: true,
    body: JSON.stringify(request),
  });

export const markChatThreadRead = (threadId: string) =>
  apiFetch<void>(`${CHAT_BASE}/${threadId}/read`, { method: "POST", authenticated: true });
