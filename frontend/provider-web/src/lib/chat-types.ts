/**
 * Chat shapes mirror the C# records in Nestly.Application.Chat
 * (ChatContracts.cs) - see provider-api's ChatController, ChatHub (live
 * delivery) and PRODUCT-ENHANCEMENTS.md "3. IN-APP CHAT" (task 193's
 * provider reply view). Field-for-field the same as customer-web's
 * lib/types.ts chat section - no JsonStringEnumConverter is registered
 * anywhere in this codebase, so both enums below serialize as their C#
 * ordinal and must mirror Nestly.Domain's declaration order exactly.
 */

/** Mirrors Nestly.Domain.ChatContextType's declaration order exactly. */
export enum ChatContextType {
  Booking = 0,
  SupportTicket = 1,
}

/** Mirrors Nestly.Domain.ChatSenderType's declaration order exactly. */
export enum ChatSenderType {
  Customer = 0,
  Admin = 1,
  Provider = 2,
}

export interface GetOrCreateChatThreadRequestBody {
  contextType: ChatContextType;
  contextId: string;
}

export interface ChatThreadResponse {
  id: string;
  contextType: ChatContextType;
  contextId: string;
  createdAtUtc: string;
  lastMessageAtUtc: string;
}

export interface SendChatMessageRequestBody {
  body: string;
}

export interface ChatMessageResponse {
  id: string;
  threadId: string;
  senderId: string;
  senderType: ChatSenderType;
  body: string;
  sentAtUtc: string;
  readAtUtc: string | null;
}

export interface ChatMessagePageResult {
  messages: ChatMessageResponse[];
  totalCount: number;
  page: number;
  pageSize: number;
}
