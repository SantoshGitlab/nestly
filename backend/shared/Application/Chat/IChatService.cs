using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Application.Chat;

/// <summary>
/// Customer-facing chat over a booking or support-ticket thread (task 191,
/// consumer-api). Every method is scoped to the caller's own customer id -
/// never a route/body parameter - same convention as BookingsController /
/// SupportTicketsController / RefundsController.
/// </summary>
public interface IChatService
{
    /// <summary>
    /// Returns the existing thread for this context, or creates one. Fails
    /// with NotFound if the booking/ticket does not exist or does not belong
    /// to <paramref name="customerId"/> - a thread can never be created for
    /// a context the caller cannot see.
    /// </summary>
    Task<Result<ChatThreadResponse>> GetOrCreateThreadAsync(Guid customerId, ChatContextType contextType, Guid contextId);

    Task<Result<ChatMessageResponse>> SendMessageAsync(Guid customerId, Guid threadId, SendChatMessageRequest request);

    /// <summary>Oldest-first page of the thread's history (task 191).</summary>
    Task<Result<ChatMessagePageResult>> GetHistoryAsync(Guid customerId, Guid threadId, int page, int pageSize);

    /// <summary>Marks every message in the thread not sent by this customer as read (task 192 read receipts).</summary>
    Task<Result> MarkReadAsync(Guid customerId, Guid threadId);
}
