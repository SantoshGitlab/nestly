using Microsoft.EntityFrameworkCore;
using Nestly.Application;
using Nestly.Application.Chat;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Provider-facing chat (task 193's provider reply view). Every method
/// resolves the thread's underlying booking and checks the caller against its
/// LIVE assignment (status Assigned or Accepted) via
/// <see cref="IBookingProviderAssignmentRepository.GetActiveByBookingAsync"/> -
/// the exact same ownership primitive <c>ProviderJobService</c> and
/// <c>BookingTrackingAuthorizer</c> use, not a re-derivation of it.
/// </summary>
public class ProviderChatService : IProviderChatService
{
    private readonly IChatThreadRepository _threadRepository;
    private readonly IChatMessageRepository _messageRepository;
    private readonly IBookingProviderAssignmentRepository _assignmentRepository;

    public ProviderChatService(
        IChatThreadRepository threadRepository,
        IChatMessageRepository messageRepository,
        IBookingProviderAssignmentRepository assignmentRepository)
    {
        _threadRepository = threadRepository;
        _messageRepository = messageRepository;
        _assignmentRepository = assignmentRepository;
    }

    public async Task<Result<ChatThreadResponse>> GetOrCreateThreadAsync(Guid providerId, ChatContextType contextType, Guid contextId)
    {
        var ownershipError = await ValidateOwnershipAsync(providerId, contextType, contextId);
        if (ownershipError is not null)
        {
            return ownershipError;
        }

        var thread = await _threadRepository.GetByContextAsync(contextType, contextId);
        if (thread is null)
        {
            thread = new ChatThread(Guid.NewGuid(), contextType, contextId);
            try
            {
                await _threadRepository.AddAsync(thread);
            }
            catch (DbUpdateException)
            {
                // Same (context_type, context_id) unique-index race ChatService
                // guards against - the customer and this provider can both
                // request the thread for the first time concurrently.
                thread = await _threadRepository.GetByContextAsync(contextType, contextId)
                    ?? throw new InvalidOperationException("Chat thread creation conflicted but no thread was found on refetch.");
            }
        }

        return Result.Success(ToThreadResponse(thread));
    }

    public async Task<Result<ChatMessageResponse>> SendMessageAsync(Guid providerId, Guid threadId, SendChatMessageRequest request)
    {
        var thread = await _threadRepository.GetByIdAsync(threadId);
        if (thread is null)
        {
            return Error.NotFound("Chat.ThreadNotFound", "The specified chat thread does not exist.");
        }

        var ownershipError = await ValidateOwnershipAsync(providerId, thread.ContextType, thread.ContextId);
        if (ownershipError is not null)
        {
            return ownershipError;
        }

        var message = new ChatMessage(
            Guid.NewGuid(), threadId, thread.ContextType, thread.ContextId, providerId, ChatSenderType.Provider, request.Body);
        await _messageRepository.AddAsync(message);

        thread.TouchLastMessage(message.SentAtUtc);
        await _threadRepository.UpdateAsync(thread);

        return Result.Success(ToMessageResponse(message));
    }

    public async Task<Result<ChatMessagePageResult>> GetHistoryAsync(Guid providerId, Guid threadId, int page, int pageSize)
    {
        var thread = await _threadRepository.GetByIdAsync(threadId);
        if (thread is null)
        {
            return Error.NotFound("Chat.ThreadNotFound", "The specified chat thread does not exist.");
        }

        var ownershipError = await ValidateOwnershipAsync(providerId, thread.ContextType, thread.ContextId);
        if (ownershipError is not null)
        {
            return ownershipError;
        }

        var (messages, totalCount) = await _messageRepository.ListByThreadAsync(threadId, page, pageSize);
        return Result.Success(new ChatMessagePageResult(messages.Select(ToMessageResponse).ToList(), totalCount, page, pageSize));
    }

    public async Task<Result> MarkReadAsync(Guid providerId, Guid threadId)
    {
        var thread = await _threadRepository.GetByIdAsync(threadId);
        if (thread is null)
        {
            return Result.Failure(Error.NotFound("Chat.ThreadNotFound", "The specified chat thread does not exist."));
        }

        var ownershipError = await ValidateOwnershipAsync(providerId, thread.ContextType, thread.ContextId);
        if (ownershipError is not null)
        {
            return Result.Failure(ownershipError);
        }

        await _messageRepository.MarkThreadReadAsync(threadId, providerId, DateTime.UtcNow);
        return Result.Success();
    }

    /// <summary>
    /// Null when the caller may proceed; otherwise the NotFound error to
    /// return (never Forbidden - existence of a booking this provider is not
    /// assigned to must not leak, same 404-not-403 rule as
    /// <c>ProviderJobService</c>/<c>BookingTrackingAuthorizer</c>).
    /// </summary>
    private async Task<Error?> ValidateOwnershipAsync(Guid providerId, ChatContextType contextType, Guid contextId)
    {
        if (contextType != ChatContextType.Booking)
        {
            // Providers have no support-ticket-context chat surface - folded
            // into the same NotFound a non-owned booking gets, not a
            // validation error, so this cannot be used to probe for which
            // context types the API distinguishes.
            return Error.NotFound("Chat.BookingNotFound", "The specified booking does not exist.");
        }

        var assignment = await _assignmentRepository.GetActiveByBookingAsync(contextId);
        if (assignment is null || assignment.ProviderId != providerId)
        {
            return Error.NotFound("Chat.BookingNotFound", "The specified booking does not exist.");
        }

        return null;
    }

    private static ChatThreadResponse ToThreadResponse(ChatThread thread) => new(
        thread.Id, thread.ContextType, thread.ContextId, thread.CreatedAtUtc, thread.LastMessageAtUtc);

    private static ChatMessageResponse ToMessageResponse(ChatMessage message) => new(
        message.Id, message.ThreadId, message.SenderId, message.SenderType, message.Body, message.SentAtUtc, message.ReadAtUtc);
}
