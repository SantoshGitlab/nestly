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

/** One row in the support-console inbox - a thread plus enough about its counterpart to triage without opening it. */
export interface AdminChatThreadSummaryResponse {
  threadId: string;
  contextType: ChatContextType;
  contextId: string;
  customerId: string;
  customerName: string;
  customerMobile: string | null;
  lastMessageAtUtc: string;
  unreadCount: number;
}

export interface AdminChatThreadListResponse {
  items: AdminChatThreadSummaryResponse[];
  totalCount: number;
  page: number;
  pageSize: number;
}
