namespace Nestly.Domain;

using Nestly.BuildingBlocks.Primitives;
using Nestly.Domain.Events;

/// <summary>
/// One append-only message in a <see cref="ChatThread"/> (PRODUCT-ENHANCEMENTS.md
/// "3. IN-APP CHAT", task 189) - the same append-only pattern as
/// <see cref="WalletLedgerEntry"/> and <c>BookingStatusHistory</c>: a message
/// is never edited or deleted after creation, so the repository intentionally
/// exposes no Update/Delete method for <see cref="Body"/>, <see cref="SenderId"/>,
/// <see cref="SenderType"/> or <see cref="SentAtUtc"/>.
///
/// <see cref="ReadAtUtc"/> is the one deliberate exception: a read receipt is
/// necessarily a later write on an already-persisted row, so it cannot be
/// "append-only" in the strict sense. It is not exposed as a general mutator
/// on this entity either, though - marking read is a narrow, bulk
/// persistence-layer operation (<c>IChatMessageRepository.MarkThreadReadAsync</c>,
/// an <c>ExecuteUpdateAsync</c> against the read_at column only) rather than
/// loading each message and calling a setter, both because a thread can hold
/// many unread messages at once and because that keeps every other field
/// truly immutable in code, not just by convention.
///
/// Not part of <see cref="ChatThread"/>'s aggregate (no owned collection) for
/// the same reason <see cref="WalletLedgerEntry"/> is not owned by a "wallet"
/// aggregate - a thread with a long history should never need to be loaded in
/// full just to append one message.
/// </summary>
public class ChatMessage : AggregateRoot<Guid>
{
    public Guid ThreadId { get; private set; }

    public ChatContextType ContextType { get; private set; }

    public Guid ContextId { get; private set; }

    public Guid SenderId { get; private set; }

    public ChatSenderType SenderType { get; private set; }

    public string Body { get; private set; } = string.Empty;

    public DateTime SentAtUtc { get; private set; }

    public DateTime? ReadAtUtc { get; private set; }

    protected ChatMessage() { }

    public ChatMessage(
        Guid id, Guid threadId, ChatContextType contextType, Guid contextId,
        Guid senderId, ChatSenderType senderType, string body)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("Message body is required.", nameof(body));
        }

        if (body.Length > 4000)
        {
            throw new ArgumentException("Message body cannot exceed 4000 characters.", nameof(body));
        }

        ThreadId = threadId;
        ContextType = contextType;
        ContextId = contextId;
        SenderId = senderId;
        SenderType = senderType;
        Body = body;
        SentAtUtc = DateTime.UtcNow;

        // Drives both the SignalR broadcast to the thread's connected
        // participants (task 190) and the offline push/SMS fallback (task
        // 194, INotificationDispatchService) - one event, two handlers,
        // same shape as every other trigger wiring in this codebase.
        RaiseDomainEvent(new ChatMessageSentEvent(Id, ThreadId, ContextType, ContextId, SenderId, SenderType, Body, SentAtUtc));
    }
}
