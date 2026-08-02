using MediatR;
using Microsoft.AspNetCore.SignalR;
using Nestly.Application.Chat;
using Nestly.Domain.Events;
using Nestly.Infrastructure.Persistence.Interceptors;

namespace Nestly.Infrastructure.Realtime;

/// <summary>
/// Pushes a newly persisted chat message to every live connection joined to
/// its thread's group (task 190). Runs in whichever process persisted the
/// message; the SignalR backplane (see ChatHub's doc comment) is what makes
/// <see cref="IHubContext{ChatHub}.Clients"/> reach connections held by the
/// *other* API process too, not just this one.
/// </summary>
public sealed class ChatHubBroadcastHandler : INotificationHandler<DomainEventNotification<ChatMessageSentEvent>>
{
    /// <summary>Client-side SignalR event name new messages arrive under.</summary>
    public const string MessageReceivedMethod = "MessageReceived";

    private readonly IHubContext<ChatHub> _hubContext;

    public ChatHubBroadcastHandler(IHubContext<ChatHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task Handle(DomainEventNotification<ChatMessageSentEvent> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;
        var payload = new ChatMessageResponse(
            domainEvent.MessageId, domainEvent.ThreadId, domainEvent.SenderId, domainEvent.SenderType,
            domainEvent.Body, domainEvent.SentAtUtc, ReadAtUtc: null);

        return _hubContext.Clients
            .Group(ChatGroups.Thread(domainEvent.ThreadId))
            .SendAsync(MessageReceivedMethod, payload, cancellationToken);
    }
}
