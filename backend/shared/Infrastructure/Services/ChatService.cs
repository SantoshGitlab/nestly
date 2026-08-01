using Microsoft.EntityFrameworkCore;
using Nestly.Application.Bookings;
using Nestly.Application.Chat;
using Nestly.Application.Support;
using Nestly.BuildingBlocks.Results;
using Nestly.Domain;

namespace Nestly.Infrastructure.Services;

/// <summary>
/// Customer-facing chat (task 191). Every method resolves the thread's
/// underlying booking/ticket and checks <c>CustomerId</c> against the caller
/// - same ownership pattern as <c>SupportTicketService</c>/<c>RefundService</c>.
/// </summary>
public class ChatService : IChatService
{
    private readonly IChatThreadRepository _threadRepository;
    private readonly IChatMessageRepository _messageRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly ISupportTicketRepository _supportTicketRepository;

    public ChatService(
        IChatThreadRepository threadRepository,
        IChatMessageRepository messageRepository,
        IBookingRepository bookingRepository,
        ISupportTicketRepository supportTicketRepository)
    {
        _threadRepository = threadRepository;
        _messageRepository = messageRepository;
        _bookingRepository = bookingRepository;
        _supportTicketRepository = supportTicketRepository;
    }

    public async Task<Result<ChatThreadResponse>> GetOrCreateThreadAsync(Guid customerId, ChatContextType contextType, Guid contextId)
    {
        var ownershipError = await ValidateOwnershipAsync(customerId, contextType, contextId);
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
                // Unique (context_type, context_id) index race - both sides
                // of the conversation requested a thread for the first time
                // concurrently. The loser refetches rather than failing.
                thread = await _threadRepository.GetByContextAsync(contextType, contextId)
                    ?? throw new InvalidOperationException("Chat thread creation conflicted but no thread was found on refetch.");
            }
        }

        return Result.Success(ToThreadResponse(thread));
    }

    public async Task<Result<ChatMessageResponse>> SendMessageAsync(Guid customerId, Guid threadId, SendChatMessageRequest request)
    {
        var thread = await _threadRepository.GetByIdAsync(threadId);
        if (thread is null)
        {
            return Error.NotFound("Chat.ThreadNotFound", "The specified chat thread does not exist.");
        }

        var ownershipError = await ValidateOwnershipAsync(customerId, thread.ContextType, thread.ContextId);
        if (ownershipError is not null)
        {
            return ownershipError;
        }

        var message = new ChatMessage(
            Guid.NewGuid(), threadId, thread.ContextType, thread.ContextId, customerId, ChatSenderType.Customer, request.Body);
        await _messageRepository.AddAsync(message);

        thread.TouchLastMessage(message.SentAtUtc);
        await _threadRepository.UpdateAsync(thread);

        return Result.Success(ToMessageResponse(message));
    }

    public async Task<Result<ChatMessagePageResult>> GetHistoryAsync(Guid customerId, Guid threadId, int page, int pageSize)
    {
        var thread = await _threadRepository.GetByIdAsync(threadId);
        if (thread is null)
        {
            return Error.NotFound("Chat.ThreadNotFound", "The specified chat thread does not exist.");
        }

        var ownershipError = await ValidateOwnershipAsync(customerId, thread.ContextType, thread.ContextId);
        if (ownershipError is not null)
        {
            return ownershipError;
        }

        var (messages, totalCount) = await _messageRepository.ListByThreadAsync(threadId, page, pageSize);
        return Result.Success(new ChatMessagePageResult(messages.Select(ToMessageResponse).ToList(), totalCount, page, pageSize));
    }

    public async Task<Result> MarkReadAsync(Guid customerId, Guid threadId)
    {
        var thread = await _threadRepository.GetByIdAsync(threadId);
        if (thread is null)
        {
            return Result.Failure(Error.NotFound("Chat.ThreadNotFound", "The specified chat thread does not exist."));
        }

        var ownershipError = await ValidateOwnershipAsync(customerId, thread.ContextType, thread.ContextId);
        if (ownershipError is not null)
        {
            return Result.Failure(ownershipError);
        }

        await _messageRepository.MarkThreadReadAsync(threadId, customerId, DateTime.UtcNow);
        return Result.Success();
    }

    /// <summary>Null when the caller may proceed; otherwise the NotFound error to return (never Forbidden - existence of another customer's thread must not leak).</summary>
    private async Task<Error?> ValidateOwnershipAsync(Guid customerId, ChatContextType contextType, Guid contextId)
    {
        switch (contextType)
        {
            case ChatContextType.Booking:
                var booking = await _bookingRepository.GetByIdAsync(contextId);
                if (booking is null || booking.CustomerId != customerId)
                {
                    return Error.NotFound("Chat.BookingNotFound", "The specified booking does not exist.");
                }

                break;

            case ChatContextType.SupportTicket:
                var ticket = await _supportTicketRepository.GetByIdAsync(contextId);
                if (ticket is null || ticket.CustomerId != customerId)
                {
                    return Error.NotFound("Chat.SupportTicketNotFound", "The specified support ticket does not exist.");
                }

                break;

            default:
                return Error.Validation("Chat.InvalidContextType", "Unsupported chat context type.");
        }

        return null;
    }

    private static ChatThreadResponse ToThreadResponse(ChatThread thread) => new(
        thread.Id, thread.ContextType, thread.ContextId, thread.CreatedAtUtc, thread.LastMessageAtUtc);

    private static ChatMessageResponse ToMessageResponse(ChatMessage message) => new(
        message.Id, message.ThreadId, message.SenderId, message.SenderType, message.Body, message.SentAtUtc, message.ReadAtUtc);
}
