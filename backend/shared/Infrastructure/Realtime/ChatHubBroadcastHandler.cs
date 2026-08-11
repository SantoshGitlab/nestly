using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
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
/// <remarks>
/// <b>The broadcast swallows its own failure</b> (task 292), exactly as
/// <see cref="BookingTrackingBroadcastHandler"/> does. Dispatch is in-process,
/// post-commit MediatR with no outbox (docs/ARCHITECTURE.md, "DOMAIN EVENT
/// DISPATCH AND DELIVERY"), so a handler that throws propagates out of
/// <c>SaveChangesAsync</c> to the caller <i>after</i> the message row is
/// already committed: the send succeeded and <c>POST .../messages</c> reports
/// failure, so the customer retries and the thread ends up with the message
/// twice. Losing the frame instead costs nothing durable - the message is in
/// the database and <c>GET /api/v1/chat/threads/{threadId}/messages</c> stays
/// the source of truth, which the client re-reads on connect and reconnect.
/// Hence no retry, no queue and no dead-letter here: this is the fast path,
/// not the only path.
/// </remarks>
public sealed class ChatHubBroadcastHandler : INotificationHandler<DomainEventNotification<ChatMessageSentEvent>>
{
    /// <summary>Client-side SignalR event name new messages arrive under.</summary>
    public const string MessageReceivedMethod = "MessageReceived";

    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<ChatHubBroadcastHandler> _logger;

    public ChatHubBroadcastHandler(IHubContext<ChatHub> hubContext, ILogger<ChatHubBroadcastHandler> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task Handle(DomainEventNotification<ChatMessageSentEvent> notification, CancellationToken cancellationToken)
    {
        var domainEvent = notification.DomainEvent;
        var payload = new ChatMessageResponse(
            domainEvent.MessageId, domainEvent.ThreadId, domainEvent.SenderId, domainEvent.SenderType,
            domainEvent.Body, domainEvent.SentAtUtc, ReadAtUtc: null);

        try
        {
            await _hubContext.Clients
                .Group(ChatGroups.Thread(domainEvent.ThreadId))
                .SendAsync(MessageReceivedMethod, payload, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown or an abandoned request, not a broadcast failure. Let it
            // out: swallowing it would turn a cooperative cancellation into a
            // silent one and hide it from the caller that asked for it. Scoped
            // to the caller's token rather than to the exception type, because
            // the backplane can raise a TaskCanceledException from its own
            // internal timeout with nobody having cancelled anything - that is
            // an infrastructure failure like any other and belongs below.
            throw;
        }
        catch (Exception exception)
        {
            // Thread and message ids only - never the body, which is
            // customer-authored free text and the one thing on this payload
            // that must never reach a log sink (docs/CODING-STANDARDS.md
            // LOGGING).
            _logger.LogWarning(
                exception,
                "Chat broadcast {BroadcastMethod} failed for thread {ThreadId} (message {MessageId}); the client will recover on its next thread read.",
                MessageReceivedMethod,
                domainEvent.ThreadId,
                domainEvent.MessageId);
        }
    }
}
