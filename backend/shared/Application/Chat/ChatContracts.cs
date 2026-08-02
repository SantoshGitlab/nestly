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
