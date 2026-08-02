namespace Nestly.Domain;

using Nestly.BuildingBlocks.Primitives;

/// <summary>
/// A messaging thread scoped to an existing booking or support ticket
/// (PRODUCT-ENHANCEMENTS.md "3. IN-APP CHAT", task 189). At most one thread
/// exists per (<see cref="ContextType"/>, <see cref="ContextId"/>) pair -
/// enforced by a unique index (see <c>ChatThreadConfiguration</c>) so the
/// service layer's get-or-create (task 191) is safe under concurrent first
/// messages from both sides of a conversation.
///
/// Unlike <see cref="ChatMessage"/>, this entity is not append-only -
/// <see cref="LastMessageAtUtc"/> is bookkeeping updated on every new
/// message so thread lists can sort by recency without a join/aggregate
/// over the message table on every read.
/// </summary>
public class ChatThread : Entity<Guid>
{
    public ChatContextType ContextType { get; private set; }

    public Guid ContextId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime LastMessageAtUtc { get; private set; }

    protected ChatThread() { }

    public ChatThread(Guid id, ChatContextType contextType, Guid contextId)
        : base(id)
    {
        ContextType = contextType;
        ContextId = contextId;
        CreatedAtUtc = DateTime.UtcNow;
        LastMessageAtUtc = CreatedAtUtc;
    }

    /// <summary>Bumps the recency marker when a new message lands (task 191's send-message flow). Never moves backwards.</summary>
    public void TouchLastMessage(DateTime sentAtUtc)
    {
        if (sentAtUtc > LastMessageAtUtc)
        {
            LastMessageAtUtc = sentAtUtc;
        }
    }
}
