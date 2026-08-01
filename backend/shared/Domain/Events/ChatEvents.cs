using Nestly.BuildingBlocks.Primitives;

namespace Nestly.Domain.Events;

/// <summary>
/// Raised when a chat message is persisted (task 189/191). Two handlers
/// subscribe to this in Infrastructure: the SignalR hub broadcast (task 190,
/// pushes to every connection joined to the thread's group) and the offline
/// notification trigger (task 194, INotificationDispatchService fallback when
/// the recipient has no live connection).
/// </summary>
public sealed record ChatMessageSentEvent(
    Guid MessageId,
    Guid ThreadId,
    ChatContextType ContextType,
    Guid ContextId,
    Guid SenderId,
    ChatSenderType SenderType,
    string Body,
    DateTime SentAtUtc) : DomainEvent;
