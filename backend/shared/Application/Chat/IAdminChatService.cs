using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Application.Chat;

/// <summary>
/// Admin support-console reply view (task 193, admin-api) - the same thread
/// model as <see cref="IChatService"/>, but not ownership-scoped to a single
/// customer: any admin holding the "chat.read" policy (see AdminModules.Chat)
/// can view and reply on any booking/support-ticket thread. Gated in the
/// controller, not here - see AdminChatController's doc comment for why this
/// module has no separate "write" tier the way most admin modules do.
/// </summary>
public interface IAdminChatService
{
    /// <summary>Fails with NotFound if the booking/ticket does not exist - unlike <see cref="IChatService"/>, no ownership check beyond that.</summary>
    Task<Result<ChatThreadResponse>> GetOrCreateThreadAsync(ChatContextType contextType, Guid contextId);

    Task<Result<ChatMessageResponse>> ReplyAsync(Guid adminUserId, Guid threadId, SendChatMessageRequest request);

    Task<Result<ChatMessagePageResult>> GetHistoryAsync(Guid threadId, int page, int pageSize);

    /// <summary>Marks every customer-authored message in the thread as read by this admin.</summary>
    Task<Result> MarkReadAsync(Guid threadId);
}
