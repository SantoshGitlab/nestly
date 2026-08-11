using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Application.Chat;

/// <summary>
/// Provider-facing chat over a booking thread (task 193's other reply view -
/// PRODUCT-ENHANCEMENTS.md "3. IN-APP CHAT" - the admin support console got
/// its own <see cref="IAdminChatService"/>, this is the provider-app/portal
/// counterpart). Every method is scoped to the caller's own provider id -
/// never a route/body parameter - same convention as <see cref="IChatService"/>
/// and provider-api's own <c>IProviderJobService</c>.
///
/// Scoped to <see cref="ChatContextType.Booking"/> only: a provider has no
/// support-ticket surface. A booking is only in scope while this provider is
/// its LIVE assignment (status Assigned or Accepted - the same
/// <c>IBookingProviderAssignmentRepository.GetActiveByBookingAsync</c> check
/// every other provider job action uses) - a provider who was rejected off,
/// reassigned away from, or never held the booking gets NotFound, identical
/// to a booking that does not exist (SRS 28.3 IDOR / 404-not-403).
/// </summary>
public interface IProviderChatService
{
    /// <summary>Returns the existing thread for this booking, or creates one. Fails with NotFound unless <paramref name="providerId"/> is the booking's current live assignment.</summary>
    Task<Result<ChatThreadResponse>> GetOrCreateThreadAsync(Guid providerId, ChatContextType contextType, Guid contextId);

    Task<Result<ChatMessageResponse>> SendMessageAsync(Guid providerId, Guid threadId, SendChatMessageRequest request);

    /// <summary>Oldest-first page of the thread's history.</summary>
    Task<Result<ChatMessagePageResult>> GetHistoryAsync(Guid providerId, Guid threadId, int page, int pageSize);

    /// <summary>Marks every message in the thread not sent by this provider as read.</summary>
    Task<Result> MarkReadAsync(Guid providerId, Guid threadId);
}
