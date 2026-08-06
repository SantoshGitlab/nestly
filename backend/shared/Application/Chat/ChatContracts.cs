using Nestly.Domain;

namespace Nestly.Application.Chat;

/// <summary>Requests the (or creates the) thread for a booking/support-ticket context (task 191).</summary>
public sealed record GetOrCreateChatThreadRequest(ChatContextType ContextType, Guid ContextId);

public sealed record ChatThreadResponse(
    Guid Id, ChatContextType ContextType, Guid ContextId, DateTime CreatedAtUtc, DateTime LastMessageAtUtc);

public sealed record SendChatMessageRequest(string Body);

public sealed record ChatMessageResponse(
    Guid Id, Guid ThreadId, Guid SenderId, ChatSenderType SenderType, string Body, DateTime SentAtUtc, DateTime? ReadAtUtc);

/// <summary>Same page-plus-total shape as <c>AdminSupportTicketSearchResult</c>.</summary>
public sealed record ChatMessagePageResult(IReadOnlyList<ChatMessageResponse> Messages, int TotalCount, int Page, int PageSize);

/// <summary>
/// One row in the admin support-console chat inbox (task 193 follow-up:
/// the console needs a starting point to reach any thread from, not just
/// the reply view for a thread it already knows the id of). Carries enough
/// about the thread's counterpart to triage without opening it - which
/// customer, which booking/ticket, how many of their messages are still
/// unread.
/// </summary>
public sealed record AdminChatThreadSummaryResponse(
    Guid ThreadId,
    ChatContextType ContextType,
    Guid ContextId,
    Guid CustomerId,
    string CustomerName,
    string? CustomerMobile,
    DateTime LastMessageAtUtc,
    int UnreadCount);

public sealed record AdminChatThreadListResponse(IReadOnlyList<AdminChatThreadSummaryResponse> Items, int TotalCount, int Page, int PageSize);
